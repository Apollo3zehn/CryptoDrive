﻿using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDrive.Core
{
    // https://docs.microsoft.com/en-us/onedrive/developer/rest-api/api/driveitem_post_content?view=odsp-graph-online
    // https://github.com/microsoftgraph/msgraph-sdk-dotnet/issues/218
    // https://github.com/microsoftgraph/msgraph-sdk-dotnet/blob/dev/tests/Microsoft.Graph.DotnetCore.Test/Requests/Functional/OneDriveTests.cs#L57
    public class OneDriveProxy : IDriveProxy
    {
        #region Events

        public event EventHandler<DriveChangedNotification> FolderChanged;

        #endregion

        #region Fields

        private Action _patch;
        private IDriveItemDeltaCollectionPage _lastDeltaPage;

        #endregion

        #region Constructors

        public OneDriveProxy(IGraphServiceClient graphServiceClient, ILogger logger, Action patch = null)
        {
             _patch = patch;

            this.GraphClient = graphServiceClient;
            this.Name = "OneDrive";
            this.Logger = logger;
        }

        #endregion

        #region Properties

        public IGraphServiceClient GraphClient { get; }

        public string Name { get; }

        private ILogger Logger { get; }

        #endregion

        #region Change Tracking

        public async Task ProcessDelta(Func<List<DriveItem>, Task> action,
                                       string folderPath,
                                       CryptoDriveContext context,
                                       SyncScope syncScope,
                                       CancellationToken cancellationToken)
        {
            var pageCounter = 0;

#warning check if folderPath != "/" would be also required
            if (syncScope != SyncScope.Full)
                throw new NotSupportedException("OneDriveProxy always provides delta pages for all drive items.");

            while (true)
            {
                using (this.Logger.BeginScope(new Dictionary<string, object>
                {
                    [$"DeltaPage ({this.Name})"] = pageCounter
                }))
                {
                    (var deltaPage, var isLast) = await this.GetDeltaPageAsync();

                    await action?.Invoke(deltaPage);
                    pageCounter++;

                    // exit while loop
                    if (isLast)
                        break;
                }
            }
        }

        private async Task<(List<DriveItem> DeltaPage, bool IsLast)> GetDeltaPageAsync()
        {
            IDriveItemDeltaCollectionPage _currentDeltaPage;

            if (_lastDeltaPage == null)
                _currentDeltaPage = await this.GraphClient.Me.Drive.Root.Delta().Request().GetAsync();
            else
                _currentDeltaPage = await _lastDeltaPage.NextPageRequest.GetAsync();

            _lastDeltaPage = _currentDeltaPage;

            // if the last page was received
            if (_currentDeltaPage.NextPageRequest == null)
            {
                var deltaLink = _currentDeltaPage.AdditionalData[Constants.OdataInstanceAnnotations.DeltaLink].ToString();
                _lastDeltaPage.InitializeNextPageRequest(this.GraphClient, deltaLink);

                return (_currentDeltaPage.ToList(), true);
            }

            return (_currentDeltaPage.ToList(), false);
        }

        #endregion

        #region CRUD

        public async Task<DriveItem> CreateOrUpdateAsync(DriveItem driveItem)
        {
            DriveItem newDriveItem;

            switch (driveItem.Type())
            {
                case DriveItemType.Folder:

                    newDriveItem = await this.GraphClient.Me.Drive.Root
                        .ItemWithPath(driveItem.GetParentPath()).Children
                        .Request()
                        .AddAsync(driveItem.ToCreateFolderDriveItem());

                    break;

                case DriveItemType.File:

                    var properties = driveItem.ToUploadableProperties();
                    var stream = driveItem.Content;

                    if (driveItem.Size <= 4 * 1024 * 1024) // file.Length <= 4 MB
                        newDriveItem = await this.UploadSmallFileAsync(driveItem.GetItemPath(), stream, properties);
                    else
                        newDriveItem = await this.UploadLargeFileAsync(driveItem.GetItemPath(), stream, properties);

                    break;

                case DriveItemType.RemoteItem:
                default:
                    throw new NotSupportedException();
            }

            return newDriveItem;
        }

        public Task<DriveItem> MoveAsync(DriveItem oldDriveItem, DriveItem newDriveItem)
        {
            throw new NotImplementedException();
        }

        public async Task DeleteAsync(DriveItem driveItem)
        {
            await this.GraphClient.Me.Drive.Items[driveItem.Id]
                .Request()
                .DeleteAsync();
        }

        #endregion

        #region File Info

        public async Task<Stream> GetContentAsync(DriveItem driveItem)
        {
            WebResponse response;

            var request = this.GraphClient.Me.Drive.Items[driveItem.Id].Request();
            var uri = driveItem.Uri();

            try
            {
                response = await WebRequest.Create(uri).GetResponseAsync();
            }
            // if download url required
            catch (Exception)
            {
#warning catch more specific error message
                this.Logger.LogWarning($"Download URI is null or has expired, requesting new one.");
                var url = (await request.Select(value => CryptoDriveConstants.DownloadUrl).GetAsync()).ToString();
                response = await WebRequest.Create(new Uri(url)).GetResponseAsync();
            }

            return response.GetResponseStream();
        }

        public Task<bool> ExistsAsync(DriveItem driveItem)
        {
            return Task.FromResult(true);
        }

        public Task<DateTime> GetLastWriteTimeUtcAsync(DriveItem driveItem)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetHashAsync(DriveItem driveItem)
        {
            throw new NotImplementedException();
        }

        public Task<DriveItem> ToFullDriveItem(DriveItem driveItem)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Private

        // https://github.com/microsoftgraph/msgraph-sdk-dotnet/issues/558
        public async Task<DriveItem> UploadSmallFileAsync(string itemPath, Stream stream, DriveItemUploadableProperties properties)
        {
            properties.ODataType = "microsoft.graph.driveItem";

            // Create http PUT request.
            var blobRequest = this.GraphClient.Me.Drive.Root.ItemWithPath(itemPath).Content.Request();

            var blob = new HttpRequestMessage(HttpMethod.Put, blobRequest.RequestUrl)
            {
                Content = new StringContent(stream.ToBase64(), Encoding.UTF8, "application/octet-stream")
            };

            // Create http PATCH request.
            var metadataRequest = this.GraphClient.Me.Drive.Root.ItemWithPath(itemPath).Request();
            var jsonString1 = this.GraphClient.HttpProvider.Serializer.SerializeObject(properties);

            var metadata = new HttpRequestMessage(HttpMethod.Patch, metadataRequest.RequestUrl)
            {
                Content = new StringContent(jsonString1, Encoding.UTF8, "application/json")
            };

            // Create batch request steps with request ids.
            var requestStep1 = new BatchRequestStep("1", blob);
            var requestStep2 = new BatchRequestStep("2", metadata, new List<string> { "1" });

            // Create batch request content.
            _patch?.Invoke();
            var batch = new BatchRequestContent();
            batch.AddBatchRequestStep(requestStep1);
            batch.AddBatchRequestStep(requestStep2);

            var response = await this.GraphClient.Batch.Request().PostAsync(batch);

            // Handle http responses using BatchResponseContent.
            var httpResponse = await response.GetResponseByIdAsync("2");
            var jsonString2 = await httpResponse.Content.ReadAsStringAsync();

            var driveItem = this.GraphClient.HttpProvider.Serializer.DeserializeObject<DriveItem>(jsonString2);

            return driveItem;
        }

        private async Task<DriveItem> UploadLargeFileAsync(string itemPath, Stream stream, DriveItemUploadableProperties properties)
        {
            var uploadSession = await this.GraphClient.Drive.Root.ItemWithPath(itemPath).CreateUploadSession(properties).Request().PostAsync();
            var maxChunkSize = 1280 * 1024; // 1280 KB - Change this to your chunk size. 5MB is the default.
            var provider = new ChunkedUploadProvider(uploadSession, this.GraphClient, stream, maxChunkSize);

            // Setup the chunk request necessities
            var chunkRequests = provider.GetUploadChunkRequests();
            var trackedExceptions = new List<Exception>();
            DriveItem driveItem = null;

            //upload the chunks
            foreach (var request in chunkRequests)
            {
                // Do your updates here: update progress bar, etc.
                // ...
                // Send chunk request
                var result = await provider.GetChunkRequestResponseAsync(request, trackedExceptions);

                if (result.UploadSucceeded)
                    driveItem = result.ItemResponse;
            }

            return driveItem;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            //
        }

        #endregion
    }
}
