using CryptoDrive.Extensions;
using Microsoft.Graph;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using File = System.IO.File;

namespace CryptoDrive.Core
{
    // https://docs.microsoft.com/en-us/onedrive/developer/rest-api/api/driveitem_post_content?view=odsp-graph-online
    // https://github.com/microsoftgraph/msgraph-sdk-dotnet/issues/218
    public class OneDriveClient : IOneDriveClient
    {
        public OneDriveClient(IGraphServiceClient graphServiceClient)
        {
            this.GraphClient = graphServiceClient;
        }

        public IGraphServiceClient GraphClient { get; }

        public async Task<DriveItem> UploadFileAsync(string localFilePath, string remoteFilePath)
        {
            var fileSystemInfo = new FileInfo(localFilePath);

            var graphFileSystemInfo = new Microsoft.Graph.FileSystemInfo()
            {
                CreatedDateTime = fileSystemInfo.CreationTimeUtc,
                LastAccessedDateTime = fileSystemInfo.LastAccessTimeUtc,
                LastModifiedDateTime = fileSystemInfo.LastWriteTimeUtc
            };

            DriveItem newDriveItem = null;

            using (var stream = File.OpenRead(localFilePath))
            {
                if (fileSystemInfo.Length <= 4 * 1024 * 1024) // file.Length <= 4 MB
                {
                    var driveItem = new DriveItem()
                    {
                        File = new Microsoft.Graph.File(),
                        FileSystemInfo = graphFileSystemInfo,
                        Name = Path.GetFileName(remoteFilePath)
                    };

                    newDriveItem = await this.UploadSmallFileAsync(driveItem, stream);
                }
                else
                {
                    var properties = new DriveItemUploadableProperties()
                    {
                        FileSystemInfo = graphFileSystemInfo
                    };

                    newDriveItem = await this.UploadLargeFileAsync(stream, properties, remoteFilePath);
                }
            }

            return newDriveItem;
        }

        public async Task<string> GetDownloadUrlAsync(string id)
        {
            return (await this.GraphClient.Me.Drive.Items[id].Request().Select(value => CryptoDriveConstants.DownloadUrl).GetAsync()).ToString();
        }

        public async Task<List<DriveItem>> GetDeltaAsync(string token = "")
        {
            // TODO: make use of NextPageRequest

            //var deltaPages = await this.GraphClient.Me.Drive.Root.Delta().Request().GetAsync();
            //var nextdelta = delta.NextPageRequest.GetAsync();

            IDriveItemDeltaCollectionPage deltaPages;

            if (string.IsNullOrWhiteSpace(token))
                deltaPages = await this.GraphClient.Me.Drive.Root.Delta().Request().GetAsync();
            else
                deltaPages = await this.GraphClient.Me.Drive.Root.Delta().Request(new List<Option> { new QueryOption("token", token) }).GetAsync();

            return deltaPages.ToList();
        }

        private async Task<DriveItem> UploadSmallFileAsync(DriveItem driveItem, Stream stream)
        {
            var blobRequest = this.GraphClient.Me.Drive.Root.ItemWithPath(driveItem.Name).Content.Request();
            var metadataRequest = this.GraphClient.Me.Drive.Root.ItemWithPath(driveItem.Name).Request();
            var baseUrl = "https://graph.microsoft.com:443/v1.0";

            var batch = new StringContent($@"
{{'requests': [
    {{
        'id': '1',
        'method': 'PUT',
        'url': '{blobRequest.RequestUrl.Substring(baseUrl.Length)}',
        'body': '{stream.ConvertToBase64()}',
        'headers': {{ 'Content-Type': 'application/octet-stream' }}
    }},
    {{
        'id': '2',
        'dependsOn': [ '1' ],
        'url': '{metadataRequest.RequestUrl.Substring(baseUrl.Length)}',
        'method': 'PATCH',
        'body': {JsonConvert.SerializeObject(driveItem)},
        'headers': {{ 'Content-Type': 'application/json' }}
    }}
]}}".Replace('\'', '"'), Encoding.UTF8, "application/json");

            var message = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/$batch")
            {
                Content = batch
            };

            var response = await this.GraphClient.HttpProvider.SendAsync(message);
            var jsonString = await response.Content.ReadAsStringAsync();
            var newDriveItem = this.GraphClient.HttpProvider.Serializer.DeserializeObject<DriveItem>(jsonString);

            return newDriveItem;
        }

        private async Task<DriveItem> UploadLargeFileAsync(Stream stream, DriveItemUploadableProperties properties, string filePath)
        {
            var uploadSession = await this.GraphClient.Drive.Root.ItemWithPath(filePath).CreateUploadSession(properties).Request().PostAsync();
            var maxChunkSize = 1280 * 1024; // 1280 KB - Change this to your chunk size. 5MB is the default.
            var provider = new ChunkedUploadProvider(uploadSession, this.GraphClient, stream, maxChunkSize);
            var chunkRequests = provider.GetUploadChunkRequests();
            var readBuffer = new byte[maxChunkSize];
            var trackedExceptions = new List<Exception>();

            DriveItem driveItem = null;

            // upload the chunks
            foreach (var request in chunkRequests)
            {
                // Do your updates here: update progress bar, etc.
                // ...
                // Send chunk request
                var result = await provider.GetChunkRequestResponseAsync(request, readBuffer, trackedExceptions);

                if (result.UploadSucceeded)
                {
                    driveItem = result.ItemResponse;
                }
            }

            // check that upload succeeded
            if (driveItem == null)
            {
                throw new Exception();
            }

            return driveItem;
        }
    }
}
