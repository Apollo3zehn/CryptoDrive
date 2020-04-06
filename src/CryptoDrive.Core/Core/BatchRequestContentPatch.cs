using HarmonyLib;
using Microsoft.Graph;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDrive.Core
{
    [HarmonyPatch(typeof(BatchRequestContent))]
    [HarmonyPatch("GetBatchRequestContentFromStepAsync")]
    public class BatchRequestContentPatch
    {
        private static bool _isApplied;

        // https://stackoverflow.com/questions/7299097/dynamically-replace-the-contents-of-a-c-sharp-method
        public static void ApplyPatch()
        {
            if (BatchRequestContentPatch._isApplied)
                return;

            var harmony = new Harmony("cryptodrive");
            harmony.PatchAll();

            BatchRequestContentPatch._isApplied = true;
        }

        internal static bool Prefix(ref Task<JObject> __result, BatchRequestStep batchRequestStep)
        {
            __result = Task.Run(async () =>
            {
                JObject jRequestContent = new JObject
                {
                    { BatchRequest.Id, batchRequestStep.RequestId },
                    { BatchRequest.Url, GetRelativeUrl(batchRequestStep.Request.RequestUri) },
                    { BatchRequest.Method, batchRequestStep.Request.Method.Method }
                };

                if (batchRequestStep.DependsOn != null && batchRequestStep.DependsOn.Count() > 0)
                    jRequestContent.Add(BatchRequest.DependsOn, new JArray(batchRequestStep.DependsOn));

                if (batchRequestStep.Request.Content?.Headers != null && batchRequestStep.Request.Content.Headers.Count() > 0)
                    jRequestContent.Add(BatchRequest.Headers, GetContentHeader(batchRequestStep.Request.Content.Headers));

                if (batchRequestStep.Request != null && batchRequestStep.Request.Content != null)
                {
                    jRequestContent.Add(BatchRequest.Body, await GetRequestContentAsync(batchRequestStep.Request));
                }

                return jRequestContent;
            });

            return false;
        }

        // Here is the workaround, everything else is just a copy from 
        // https://github.com/microsoftgraph/msgraph-sdk-dotnet-core/blob/bc4c88746370f3c532fa22cf8677683f3ef1f14d/src/Microsoft.Graph.Core/Exceptions/ErrorConstants.cs
        private static async Task<JToken> GetRequestContentAsync(HttpRequestMessage request)
        {
            try
            {
                // base64
                if (request.Content is StringContent
                    && request.Content.Headers.ContentType.MediaType == MediaTypeNames.Application.Octet)
                {
                    return await request.Content.ReadAsStringAsync();
                }
                // json
                else
                {
                    using (Stream streamContent = await request.Content.ReadAsStreamAsync())
                    {
                        return new Serializer().DeserializeObject<JObject>(streamContent);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ClientException(new Error
                {
                    Code = ErrorConstants.Codes.InvalidRequest,
                    Message = ErrorConstants.Messages.UnableToDeserializexContent
                }, ex);
            }
        }

        private static string GetRelativeUrl(Uri requestUri)
        {
            string version = "v1.0";
            if (requestUri.AbsoluteUri.Contains("beta"))
                version = "beta";

            return requestUri.AbsoluteUri.Substring(requestUri.AbsoluteUri.IndexOf(version) + version.ToCharArray().Count());
        }

        private static JObject GetContentHeader(HttpContentHeaders headers)
        {
            JObject jHeaders = new JObject();
            foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
            {
                jHeaders.Add(header.Key, GetHeaderValuesAsString(header.Value));
            }
            return jHeaders;
        }

        private static string GetHeaderValuesAsString(IEnumerable<string> headerValues)
        {
            if (headerValues == null || headerValues.Count() == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            foreach (string headerValue in headerValues)
            {
                builder.Append(headerValue);
            }

            return builder.ToString();
        }

        public static class BatchRequest
        {
            internal const string Id = "id";
            internal const string Url = "url";
            internal const string Body = "body";
            internal const string DependsOn = "dependsOn";
            internal const string Method = "method";
            internal const string Requests = "requests";
            internal const string Responses = "responses";
            internal const string Status = "status";
            internal const string Headers = "headers";
        }

        internal static class ErrorConstants
        {
            internal static class Codes
            {
                internal static string InvalidRequest = "invalidRequest";
            }

            internal static class Messages
            {
                internal static string UnableToDeserializexContent = "Unable to deserialize content.";
            }
        }
    }
}
