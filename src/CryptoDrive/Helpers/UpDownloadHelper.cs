using Microsoft.Graph;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDrive.Helpers
{
    public class UpDownloadHelper
    {

        public static async Task UploadSmallFile(GraphServiceClient graphClient, DriveItem driveItem, Stream stream)
        {
            var jsondata = JsonConvert.SerializeObject(driveItem, Formatting.Indented);

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

        public static async Task OneDriveUploadLargeFile(GraphServiceClient graphClient, Stream stream, DriveItemUploadableProperties properties, string filePath)
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
