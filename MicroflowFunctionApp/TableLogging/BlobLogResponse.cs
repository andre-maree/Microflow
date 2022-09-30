using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using MicroflowModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using static MicroflowModels.Constants;

namespace Microflow.Logging
{
    public static class BlobLogResponseActivity
    {
        [FunctionName(CallNames.LogMicroflowHttpData)]
        public static async Task BlobLogResponse([ActivityTrigger] (string blobName, string data, bool isRequest) input)
        {
            string prefix = input.isRequest ? "request-" : "response-";

            BlobContainerClient blobContainerClient = new BlobContainerClient("UseDevelopmentStorage=true", "microflow-httpdata");

            try
            {

                //blobContainerClient.CreateIfNotExists();

                await blobContainerClient.UploadBlobAsync(prefix + input.blobName, BinaryData.FromString(input.data));
                //var blobServiceClient = new BlobServiceClient("AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;");
                //await logEntity.LogStep();
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                await blobContainerClient.CreateIfNotExistsAsync();

                await BlobLogResponse(input);
            }
        }

        private static async Task CreateContainer(BlobContainerClient blobContainerClient)
        {
            await blobContainerClient.CreateIfNotExistsAsync();
        }
    }
}