using System;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using static MicroflowModels.Constants;

namespace Microflow.Logging
{
    public static class BlobLogResponseActivity
    {
        [FunctionName(CallNames.LogMicroflowHttpData)]
        public static async Task BlobLogResponse([ActivityTrigger] (string blobName, string data, bool isRequest) input)
        {
            string prefix = input.isRequest ? "request-" : "response-";

            BlobContainerClient blobContainerClient = new("UseDevelopmentStorage=true", "microflow-httpdata");

            try
            {
                await blobContainerClient.UploadBlobAsync(prefix + input.blobName, BinaryData.FromString(input.data));
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                await blobContainerClient.CreateIfNotExistsAsync();

                await BlobLogResponse(input);
            }
        }
    }
}