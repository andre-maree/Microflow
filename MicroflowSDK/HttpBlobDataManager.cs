using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroflowSDK
{
    public static class HttpBlobDataManager
    {
        public static async Task<string> GetHttpBlob(bool isRequest, string workflowName, int stepNumber, string runId, string subinstanceId)
        {
            string prefix = isRequest ? "request-" : "response-";

            BlobContainerClient blobContainerClient = new("UseDevelopmentStorage=true", "microflow-httpdata");

            var blobClient = blobContainerClient.GetBlobClient($"{prefix}{workflowName}@{stepNumber}@{runId}@{subinstanceId}");

            BlobDownloadResult downloadResult = await blobClient.DownloadContentAsync();

            return downloadResult.Content.ToString();
        }
    }
}
