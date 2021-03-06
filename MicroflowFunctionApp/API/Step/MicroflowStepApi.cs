using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MicroflowModels;
using MicroflowModels.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace Microflow.Api.Step
{
    public static class MicroflowStepApi
    {
        /// <summary>
        /// Get a Microflow step config
        /// </summary>
        [FunctionName("GetStep")]
        public static async Task<HttpResponseMessage> GetMicroflowStep([HttpTrigger(AuthorizationLevel.Anonymous, "get",
                                                                      Route = Constants.MicroflowPath + "/GetStep/{workflowName}/{stepNumber}")] HttpRequestMessage req,
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
        /// Upsert a complete Microflow step config
        /// </summary>
        [FunctionName("UpsertStep")]
        public static async Task<HttpResponseMessage> UpsertMicroflowStep([HttpTrigger(AuthorizationLevel.Anonymous, "put",
                                                                      Route = Constants.MicroflowPath + "/UpsertStep")] HttpRequestMessage req)
        {
            HttpCallWithRetries step = JsonSerializer.Deserialize<HttpCallWithRetries>(await req.Content.ReadAsStringAsync());

            await step.UpsertStep();

            return new(HttpStatusCode.OK);
        }

        /// <summary>
        /// Update Microflow step config properties
        /// </summary>
        [FunctionName("SetStepProperties")]
        public static async Task<HttpResponseMessage> SetMicroflowStepProperties([HttpTrigger(AuthorizationLevel.Anonymous, "put",
                                                                      Route = Constants.MicroflowPath + "/SetStepProperties/{workflowName}/{stepNumber}")] HttpRequestMessage req,
                                                                      string workflowName, string stepNumber)
        {
            List<KeyValuePair<string, object>> kvpList = JsonSerializer.Deserialize<List<KeyValuePair<string, object>>>(await req.Content.ReadAsStringAsync());

            Type t = typeof(HttpCallWithRetries);

            HttpCallWithRetries step = await new MicroflowRun()
            {
                WorkflowName = workflowName,
                RunObject = new RunObject()
                {
                    StepNumber = stepNumber
                }
            }.GetStep();

            foreach(var kvp in kvpList)
            {
                var prop = t.GetProperty(kvp.Key);

                if (prop.PropertyType == typeof(int))
                {
                    prop.SetValue(step, Convert.ToInt32(kvp.Value));
                }
                else if (prop.PropertyType == typeof(bool))
                {
                    prop.SetValue(step, Convert.ToBoolean(kvp.Value));
                }
                else
                {
                    prop.SetValue(step, kvp.Value.ToString());
                }
            }

            await step.UpsertStep();

            return new(HttpStatusCode.OK);
        }
    }
}
