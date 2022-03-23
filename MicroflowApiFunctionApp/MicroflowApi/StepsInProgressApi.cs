using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using static MicroflowModels.Constants.Constants;

namespace MicroflowApi
{
    public class StepsInProgressApi
    {
        //private readonly ILogger<StepsInProgressApi> _logger;

        //public StepsInProgressApi(ILogger<StepsInProgressApi> log)
        //{
        //    _logger = log;
        //}

        //[OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        //[OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        //[OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        //[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        [FunctionName("GetStepsCountInProgress")]
        public static async Task<int> GetStepsCountInProgress([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetStepsCountInProgress/{workflowNameStepNumber}")] HttpRequestMessage req,
                                                             [DurableClient] IDurableEntityClient client,
                                                             string workflowNameStepNumber)
        {
            EntityId countId = new EntityId("StepCount", workflowNameStepNumber);

            EntityStateResponse<int> result = await client.ReadEntityStateAsync<int>(countId);

            return result.EntityState;
        }
    }
}

