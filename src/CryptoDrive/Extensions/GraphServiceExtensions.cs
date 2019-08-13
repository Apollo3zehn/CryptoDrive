using Microsoft.Graph;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

// https://docs.microsoft.com/en-us/onedrive/developer/rest-api/api/driveitem_post_content?view=odsp-graph-online
// https://github.com/microsoftgraph/msgraph-sdk-dotnet/issues/218

namespace CryptoDrive.Extensions
{
    public static class GraphServiceClientExtensions
    {
        public static async Task UploadSmallFile4(this GraphServiceClient graphClient, DriveItem driveItem, Stream stream, string filePath)
        {
            var blobRequest = graphClient.Me.Drive.Root.ItemWithPath(filePath).Content.Request();
            var metadataRequest = graphClient.Me.Drive.Root.ItemWithPath(filePath).Request();
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

            var response = await graphClient.HttpProvider.SendAsync(message);
        }


        public static async Task UploadSmallFile3(this GraphServiceClient graphClient, DriveItem driveItem, Stream stream, string filePath)
        {
            // Create http PUT request.
            var blobRequest = graphClient.Me.Drive.Root.ItemWithPath(filePath).Content.Request();

            var blob = new HttpRequestMessage(HttpMethod.Put, blobRequest.RequestUrl)
            {
                Content = new StreamContent(stream)
            };

            // Create http PATCH request.
            var metadataRequest = graphClient.Me.Drive.Root.ItemWithPath(filePath).Request();
            var jsonContent = graphClient.HttpProvider.Serializer.SerializeObject(driveItem);

            var metadata = new HttpRequestMessage(HttpMethod.Patch, metadataRequest.RequestUrl)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };

            // Create batch request steps with request ids.
            var requestStep1 = new BatchRequestStep("1", blob, null);
            var requestStep2 = new BatchRequestStep("2", metadata, new List<string> { "1" });

            // Add batch request steps to BatchRequestContent.
            var batch = new BatchRequestContent();
            batch.AddBatchRequestStep(requestStep1);
            batch.AddBatchRequestStep(requestStep2);

            // Create the top-level HttpRequestMessage.
            var message = new HttpRequestMessage(HttpMethod.Put, "https://graph.microsoft.com/v1.0/$batch")
            {
                Content = batch
            };

            var response = await graphClient.HttpProvider.SendAsync(message);

            // Handle http responses using BatchResponseContent.
            var batchResponseContent = new BatchResponseContent(response);
            var responses = await batchResponseContent.GetResponsesAsync();
            var httpResponse = await batchResponseContent.GetResponseByIdAsync("1");
            string nextLink = await batchResponseContent.GetNextLinkAsync();
        }

        public static async Task UploadSmallFile2(this GraphServiceClient graphClient, DriveItem driveItem, Stream stream)
        {
            var jsondata = graphClient.HttpProvider.Serializer.SerializeObject(driveItem);

            // Create the metadata part. 
            StringContent stringContent = new StringContent(jsondata, Encoding.UTF8, "application/json");
            stringContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("related");
            stringContent.Headers.ContentDisposition.Name = "Metadata";
            stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // Create the data part.
            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("related");
            streamContent.Headers.ContentDisposition.Name = "Data";
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

            // Put the multiparts together
            string boundary = "MultiPartBoundary32541";
            MultipartContent multiPartContent = new MultipartContent("related", boundary);
            multiPartContent.Add(stringContent);
            multiPartContent.Add(streamContent);

            var requestUrl = graphClient.Me.Drive.Items["F4C4DC6C33B9D421!103"].Children.Request().RequestUrl;

            // Create the request message and add the content.
            HttpRequestMessage hrm = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            hrm.Content = multiPartContent;

            // Send the request and get the response.
            var response = await graphClient.HttpProvider.SendAsync(hrm);
        }

        public static async Task UploadSmallFile(this GraphServiceClient graphClient, DriveItem driveItem, Stream stream, string filePath)
        {
            var newDriveItem = await graphClient.Me.Drive.Root.ItemWithPath(filePath).Content.Request().PutAsync<DriveItem>(stream);
            await graphClient.Me.Drive.Items[newDriveItem.Id].Request().UpdateAsync(driveItem);
        }

        public static async Task OneDriveUploadLargeFile(this GraphServiceClient graphClient, Stream stream, DriveItemUploadableProperties properties, string filePath)
        {
            var uploadSession = await graphClient.Drive.Root.ItemWithPath(filePath).CreateUploadSession(properties).Request().PostAsync();
            var maxChunkSize = 1280 * 1024; // 1280 KB - Change this to your chunk size. 5MB is the default.
            var provider = new ChunkedUploadProvider(uploadSession, graphClient, stream, maxChunkSize);
            var chunkRequests = provider.GetUploadChunkRequests();
            var readBuffer = new byte[maxChunkSize];
            var trackedExceptions = new List<Exception>();

            DriveItem itemResult = null;

            // upload the chunks
            foreach (var request in chunkRequests)
            {
                // Do your updates here: update progress bar, etc.
                // ...
                // Send chunk request
                var result = await provider.GetChunkRequestResponseAsync(request, readBuffer, trackedExceptions);

                if (result.UploadSucceeded)
                {
                    itemResult = result.ItemResponse;
                }
            }

            // check that upload succeeded
            if (itemResult == null)
            {
                // Retry the upload
                // ...
            }
        }
    }
}