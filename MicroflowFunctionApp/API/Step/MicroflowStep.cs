using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MicroflowModels;
using MicroflowModels.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using static MicroflowModels.Constants;

namespace Microflow.Api.Step
{
    public static class MicroflowStepApi
    {
        /// <summary>
        /// Get a Microflow step config
        /// </summary>
        [FunctionName("GetMicroflowStep")]
        public static async Task<HttpResponseMessage> GetMicroflowStep([HttpTrigger(AuthorizationLevel.Anonymous, "get",
                                                                      Route = "GetMicroflowStep/{workflowName}/{stepNumber}")] HttpRequestMessage req,
                                                                      string workflowName, string stepNumber)
        {
            HttpCallWithRetries step = await new MicroflowRun()
            {
                WorkflowName = workflowName,
                RunObject = new RunObject()
                {
                    StepNumber = stepNumber
                }
            }.GetStep();

            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(step))
            };
        }

        /// <summary>
        /// Upsert a Microflow step config
        /// </summary>
        [FunctionName("UpsertMicroflowStep")]
        public static async Task<HttpResponseMessage> UpsertMicroflowStep([HttpTrigger(AuthorizationLevel.Anonymous, "put",
                                                                      Route = "UpsertMicroflowStep/{workflowName}/{stepNumber}")] HttpRequestMessage req,
                                                                      string workflowName, string stepNumber)
        {
            HttpCallWithRetries step = JsonSerializer.Deserialize<HttpCallWithRetries>(await req.Content.ReadAsStringAsync());

            await step.UpsertStep();

            return new(HttpStatusCode.OK);
        }
    }
}
