using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microflow.Helpers;
using MicroflowModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace MicroflowApiFunctionApp
{
    public static class ProjectApi
    {
        //[FunctionName("ProjectApi")]
        //[OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        //[OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        //[OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        //[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        //public static async Task<IActionResult> Run(
        //    [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
        //    ILogger log)
        //{
        //    log.LogInformation("C# HTTP trigger function processed a request.");

        //    string name = req.Query["name"];

        //    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        //    dynamic data = JsonConvert.DeserializeObject(requestBody);
        //    name = name ?? data?.name;

        //    string responseMessage = string.IsNullOrEmpty(name)
        //        ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
        //        : $"Hello, {name}. This HTTP triggered function executed successfully.";

        //    return new OkObjectResult(responseMessage);
        //}

        /// <summary>
        /// Call this to get the Json data of the project
        /// </summary>
        [FunctionName("GetProjectJsonWithMergefieldsReplaced")]
        public static async Task<string> GetProjectJsonWithMergefieldsReplaced([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "GetProjectJsonWithMergefieldsReplaced/{projectName}")] HttpRequestMessage req,
                                                        string projectName)
        {
            return await Task.Run(() => MicroflowTableHelper.GetProjectAsJson(projectName));
        }

        /// <summary>
        /// Call this to get the Json data of the project
        /// </summary>
        [FunctionName("GetProjectJsonAsLastSaved")]
        public static async Task<HttpResponseMessage> GetProjectJsonAsLastSaved([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "GetProjectJsonAsLastSaved/{projectName}")] HttpRequestMessage req,
                                                        string projectName)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("MicroflowStorage"));

            // Create the container and return a container client object
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("microflow-projects");
            await containerClient.CreateIfNotExistsAsync();

            // Get a reference to a blob
            BlobClient blobClient = containerClient.GetBlobClient(projectName);

            BlobDownloadInfo download = await blobClient.DownloadAsync();

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Content = new StreamContent(download.Content);

            return resp;
        }

        /// <summary>
        /// Call this to get the Json data of the project
        /// </summary>
        [FunctionName("SaveProjectJson")]
        public static async Task<HttpResponseMessage> SaveProjectJson([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "SaveProjectJson")] HttpRequestMessage req)
        {
            string content = await req.Content.ReadAsStringAsync();
            MicroflowProject microflowProject = JsonSerializer.Deserialize<MicroflowProject>(content);

            // Create a BlobServiceClient object which will be used to create a container client
            BlobServiceClient blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("MicroflowStorage"));

            // Create the container and return a container client object
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("microflow-projects");
            await containerClient.CreateIfNotExistsAsync();

            // Get a reference to a blob
            BlobClient blobClient = containerClient.GetBlobClient(microflowProject.ProjectName);

            byte[] str = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(microflowProject));
            
            try 
            {
                using (MemoryStream ms = new MemoryStream(str))
                  
                    await blobClient.UploadAsync(ms, overwrite: true);
            }
            catch (Exception ex)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);

        }
    }
}