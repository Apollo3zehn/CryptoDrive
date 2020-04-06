using CryptoDrive.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using File = System.IO.File;

namespace CryptoDrive.Core
{
    // https://docs.microsoft.com/en-us/onedrive/developer/rest-api/api/driveitem_post_content?view=odsp-graph-online
    // https://github.com/microsoftgraph/msgraph-sdk-dotnet/issues/218
    // https://github.com/microsoftgraph/msgraph-sdk-dotnet/blob/dev/tests/Microsoft.Graph.DotnetCore.Test/Requests/Functional/OneDriveTests.cs#L57
    public class OneDriveProxy : IDriveProxy
    {
        #region Events

        public event EventHandler<string> FolderChanged;

        #endregion

        #region Fields

        private Action _patch;
        private IDriveItemDeltaCollectionPage _lastDeltaPage;

        #endregion

        #region Constructors

        public OneDriveProxy(IGraphServiceClient graphServiceClient, ILogger logger, Action patch = null)
        {
            _patch = null;

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
                                       CancellationToken cancellationToken)
        {
            var pageCounter = 0;

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
            DriveItem newDriveItem = null;
            var sourceFilePath = HttpUtility.UrlDecode(driveItem.Uri().AbsolutePath);

            using (var stream = File.OpenRead(sourceFilePath))
            {
                if (driveItem.Size <= 4 * 1024 * 1024) // file.Length <= 4 MB
                {
                    newDriveItem = await this.UploadSmallFileAsync(driveItem, stream);
                }
                else
                {
                    var properties = new DriveItemUploadableProperties()
                    {
                        FileSystemInfo = driveItem.FileSystemInfo
                    };

                    newDriveItem = await this.UploadLargeFileAsync(stream, properties, driveItem.GetItemPath());
                }
            }

            return newDriveItem;
        }

        public Task<DriveItem> MoveAsync(DriveItem oldDriveItem, DriveItem newDriveItem)
        {
            throw new NotImplementedException();
        }

        public Task<DriveItem> DeleteAsync(DriveItem driveItem)
        {
            return Task.FromResult(driveItem);
        }

        #endregion

        #region File Info

        public async Task<Uri> GetDownloadUriAsync(DriveItem driveItem)
        {
            var url = (await this.GraphClient.Me.Drive.Items[driveItem.Id].Request().Select(value => CryptoDriveConstants.DownloadUrl).GetAsync()).ToString();

            return new Uri(url);
        }

        public Task<bool> ExistsAsync(DriveItem driveItem)
        {
            throw new NotImplementedException();
        }

        public Task<DateTime> GetLastWriteTimeUtcAsync(DriveItem driveItem)
        {
            throw new NotImplementedException();
        }

        public Task SetLastWriteTimeUtcAsync(DriveItem driveItem)
        {
            //throw new NotImplementedException();
            return Task.CompletedTask;
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
        public async Task<DriveItem> UploadSmallFileAsync(DriveItem driveItem, Stream stream)
        {
            // Create http PUT request.
            var blobRequest = this.GraphClient.Me.Drive.Root.ItemWithPath(driveItem.GetItemPath()).Content.Request();

            var blob = new HttpRequestMessage(HttpMethod.Put, blobRequest.RequestUrl)
            {
                Content = new StringContent(stream.ToBase64(), Encoding.UTF8, "application/octet-stream")
            };

            // Create http PATCH request.
            var metadataRequest = this.GraphClient.Me.Drive.Root.ItemWithPath(driveItem.GetItemPath()).Request();
            var jsonContent = this.GraphClient.HttpProvider.Serializer.SerializeObject(driveItem);

            var metadata = new HttpRequestMessage(HttpMethod.Patch, metadataRequest.RequestUrl)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
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
            //var responses = await response.GetResponsesAsync();
            //var httpResponse = await response.GetResponseByIdAsync("1");
            //string nextLink = await response.GetNextLinkAsync();

            return driveItem;
        }

        private async Task<DriveItem> UploadLargeFileAsync(Stream stream, DriveItemUploadableProperties properties, string filePath)
        {
            var uploadSession = await this.GraphClient.Drive.Root.ItemWithPath(filePath).CreateUploadSession(properties).Request().PostAsync();
            var maxChunkSize = 1280 * 1024; // 1280 KB - Change this to your chunk size. 5MB is the default.
            var provider = new ChunkedUploadProvider(uploadSession, this.GraphClient, stream, maxChunkSize);

            // Setup the chunk request necessities
            var chunkRequests = provider.GetUploadChunkRequests();
            var trackedExceptions = new List<Exception>();
            DriveItem itemResult = null;

            //upload the chunks
            foreach (var request in chunkRequests)
            {
                // Do your updates here: update progress bar, etc.
                // ...
                // Send chunk request
                var result = await provider.GetChunkRequestResponseAsync(request, trackedExceptions);

                if (result.UploadSucceeded)
                {
                    itemResult = result.ItemResponse;
                }
            }

            return itemResult;
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
