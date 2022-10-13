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
        public static async Task<string> GetHttpBlob(string workflowName, int stepNumber, string runId, string subinstanceId)
        {
            BlobContainerClient blobContainerClient = new("UseDevelopmentStorage=true", "microflow-httpdata");

            var blobClient = blobContainerClient.GetBlobClient($"response-{workflowName}@{stepNumber}@{runId}@{subinstanceId}");

            BlobDownloadResult downloadResult = await blobClient.DownloadContentAsync();

            return downloadResult.Content.ToString();
        }
    }
}
