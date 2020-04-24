using CryptoDrive.Core;
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

namespace CryptoDrive.Drives
{
    public class OneDriveProxy : IDriveProxy
    {
        #region Events

        public event EventHandler<DriveChangedNotification> FolderChanged;

        #endregion

        #region Fields

        private readonly string _basePrefix;
        private Action _patch;
        private IDriveItemDeltaCollectionPage _lastDeltaPage;

        #endregion

        #region Constructors

        private OneDriveProxy(string basePath,
                              IGraphServiceClient graphClient,
                              OneDriveAccountType accountType,
                              ILogger logger,
                              Action patch = null)
        {
            if (basePath == "/")
                _basePrefix = $"{OneDriveConstants.RootPrefix}";
            else
                _basePrefix = $"{OneDriveConstants.RootPrefix}{basePath}";

            _patch = patch;

            this.Name = "OneDrive";
            this.BasePath = basePath;
            this.GraphClient = graphClient;
            this.AccountType = accountType;
            this.Logger = logger;
        }

        #endregion

        #region Properties

        public IGraphServiceClient GraphClient { get; }

        public OneDriveAccountType AccountType { get; }

        public string Name { get; }

        public string BasePath { get; }

        private ILogger Logger { get; }

        #endregion

        #region Methods

        public static Task<OneDriveProxy> CreateAsync(IGraphServiceClient graphClient,
                                                      OneDriveAccountType accountType,
                                                      ILogger logger,
                                                      Action patch = null)
        {
            return OneDriveProxy.CreateAsync("/", graphClient, accountType, logger, patch);
        }

        public static async Task<OneDriveProxy> CreateAsync(string basePath,
                                                            IGraphServiceClient graphClient,
                                                            OneDriveAccountType accountType,
                                                            ILogger logger,
                                                            Action patch = null)
        {
            var drive = new OneDriveProxy(basePath, graphClient, accountType, logger, patch);
            await drive.InitializeAsync();

            return drive;
        }

        private async Task InitializeAsync()
        {
            // ensure base folder exists (except it is root)
            if (this.BasePath != "/")
            {
                var driveItem = this.BasePath.ToDriveItem(DriveItemType.Folder);
                var absoluteParentPath = driveItem.Path;
                var createFolderDriveItem = this.CreateFolderDriveItem(driveItem.Name);

                await this.GraphClient.GetDriveItemRequestBuilder(absoluteParentPath).Children
                    .Request()
                    .AddAsync(createFolderDriveItem);
            }
        }

        #endregion

        #region Navigation

        public async Task<List<CryptoDriveItem>> GetFolderContentAsync(CryptoDriveItem driveItem)
        {
            IDriveItemChildrenCollectionRequest request;

            if (string.IsNullOrWhiteSpace(driveItem.Id))
                request = this.GraphClient
                    .GetDriveItemRequestBuilder(driveItem.GetItemPath()).Children
                    .Request();
            else
                request = this.GraphClient.Me.Drive.Items[driveItem.Id].Children
                    .Request();

            var driveItems = (await request
                    .GetAsync())
                    .Where(driveItem => driveItem.File != null || driveItem.Folder != null)
                    .Select(driveItem => driveItem.ToCryptoDriveItem(_basePrefix))
                    .ToList();

            return driveItems;
        }

        #endregion

        #region Change Tracking

        public async Task ProcessDelta(Func<List<CryptoDriveItem>, Task> action,
                                       string folderPath,
                                       CryptoDriveContext context,
                                       DriveChangedType changeType,
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

        private async Task<(List<CryptoDriveItem> DeltaPage, bool IsLast)> GetDeltaPageAsync()
        {
            IDriveItemDeltaRequest deltaRequest;

            if (_lastDeltaPage == null)
            {
                if (this.AccountType == OneDriveAccountType.Personal)
                {
                    // according to MS (https://docs.microsoft.com/en-us/graph/api/driveitem-delta?view=graph-rest-1.0&tabs=csharp#remarks)
                    // ...Root.ItemWithPath() only works with personal accounts
                    deltaRequest = this.GraphClient.GetDriveItemRequestBuilder(this.BasePath)
                        .Delta()
                        .Request();
                }
                else
                {
                    // according to MS (https://docs.microsoft.com/en-us/graph/api/driveitem-delta?view=graph-rest-1.0&tabs=csharp#remarks)
                    // ...Root.ItemWithPath() only works with personal accounts
                    deltaRequest = this.GraphClient.Me.Drive.Root
                        .Delta()
                        .Request();
                }
            }
            else
            {
                deltaRequest = _lastDeltaPage.NextPageRequest;
            }

            var currentDeltaPage = await deltaRequest.GetAsync();
            _lastDeltaPage = currentDeltaPage;

            var deltaPage = currentDeltaPage.Where(driveItem =>
            {
                var itemPath = CoreUtilities.PathCombine(driveItem.ParentReference.Path, driveItem.Name);

                // exclude no-name items
                if (string.IsNullOrWhiteSpace(driveItem.Name))
                    return false;

                // exlude items that are located above the root folder (only for work or school accounts)
                if (this.AccountType == OneDriveAccountType.WorkOrSchool &&
                   !CoreUtilities.IsAncestorOf(_basePrefix, itemPath))
                    return false;

                // exclude base items
                return (driveItem.File != null || driveItem.Folder != null) && 
                        driveItem.Root == null &&
                        itemPath != _basePrefix;
            }).ToList();

            var convertedDeltaPage = deltaPage.Select(driveItem => driveItem.ToCryptoDriveItem(_basePrefix)).ToList();

            // if the last page was received
            if (currentDeltaPage.NextPageRequest == null)
            {
                var deltaLink = currentDeltaPage.AdditionalData[Constants.OdataInstanceAnnotations.DeltaLink].ToString();
                _lastDeltaPage.InitializeNextPageRequest(this.GraphClient, deltaLink);

                return (convertedDeltaPage, true);
            }
            else
            {
                return (convertedDeltaPage, false);
            }
        }

        #endregion

        #region CRUD

        public async Task<CryptoDriveItem> CreateOrUpdateAsync(CryptoDriveItem driveItem, Stream content, CancellationToken cts)
        {
            DriveItem newDriveItem;

            switch (driveItem.Type)
            {
                case DriveItemType.Folder:

                    var absoluteParentPath = driveItem.Path.ToAbsolutePath(this.BasePath);
                    var createFolderDriveItem = this.CreateFolderDriveItem(driveItem.Name);

                    newDriveItem = await this.GraphClient.GetDriveItemRequestBuilder(absoluteParentPath).Children
                        .Request()
                        .AddAsync(createFolderDriveItem);

                    break;

                case DriveItemType.File:

                    var properties = this.CreateUploadableProperties(driveItem.LastModified);
                    var absoluteItemPath = driveItem.GetAbsolutePath(this.BasePath);

                    if (driveItem.Size <= 4 * 1024 * 1024) // file.Length <= 4 MB
                        newDriveItem = await this.UploadSmallFileAsync(absoluteItemPath, content, properties, cts);
                    else
                        newDriveItem = await this.UploadLargeFileAsync(absoluteItemPath, content, properties, cts);

                    break;

                default:
                    throw new NotSupportedException();
            }

            return newDriveItem.ToCryptoDriveItem(_basePrefix);
        }

        public Task<CryptoDriveItem> MoveAsync(CryptoDriveItem oldDriveItem, CryptoDriveItem newDriveItem)
        {
            throw new NotImplementedException();
        }

        public async Task DeleteAsync(CryptoDriveItem driveItem)
        {
            await this.GraphClient.Me.Drive.Items[driveItem.Id]
                .Request()
                .DeleteAsync();
        }

        #endregion

        #region File Info

        public async Task<Stream> GetFileContentAsync(CryptoDriveItem driveItem)
        {
            IDriveItemContentRequest request;

            if (string.IsNullOrWhiteSpace(driveItem.Id))
                request = this.GraphClient
                    .GetDriveItemRequestBuilder(driveItem.GetItemPath()).Content
                    .Request();
            else
                request = this.GraphClient.Me.Drive.Items[driveItem.Id].Content
                    .Request();

            var content = await request.GetAsync();

            return content;
        }

        public Task<bool> ExistsAsync(CryptoDriveItem driveItem)
        {
            return Task.FromResult(true);
        }

        #endregion

        #region Private

        private DriveItemUploadableProperties CreateUploadableProperties(DateTime lastModified)
        {
            return new DriveItemUploadableProperties()
            {
                FileSystemInfo = new Microsoft.Graph.FileSystemInfo() { LastModifiedDateTime = lastModified }
            };
        }

        private DriveItem CreateFolderDriveItem(string folderName)
        {
            return new DriveItem
            {
                Name = folderName,
                Folder = new Folder(),
                AdditionalData = new Dictionary<string, object>()
                {
                    {"@microsoft.graph.conflictBehavior", "replace"}
                }
            };
        }

        // https://github.com/microsoftgraph/msgraph-sdk-dotnet/issues/558
        private async Task<DriveItem> UploadSmallFileAsync(string itemPath, Stream stream, DriveItemUploadableProperties properties, CancellationToken cts)
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

            var response = await this.GraphClient.Batch.Request().PostAsync(batch, cts);
            var driveItem = await response.GetResponseByIdAsync<DriveItem>("2");

            return driveItem;
        }

        private async Task<DriveItem> UploadLargeFileAsync(string itemPath, Stream stream, DriveItemUploadableProperties properties, CancellationToken cts)
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
                cts.ThrowIfCancellationRequested();

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
