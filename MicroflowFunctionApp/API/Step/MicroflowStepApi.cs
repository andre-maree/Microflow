using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microflow.Helpers;
using MicroflowModels;
using MicroflowModels.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using static MicroflowModels.Constants;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace Microflow.Api.Step
{
    public static class MicroflowStepApi
    {
        /// <summary>
        /// Get a Microflow step config
        /// </summary>
        [FunctionName("RunFromStep")]
        public static async Task<HttpResponseMessage> RunFromStep([HttpTrigger(AuthorizationLevel.Anonymous, "get",
                                                                      Route = MicroflowPath + "/runFromStep/{workflowName}/{stepNumber}/{globalKey?}")] HttpRequestMessage req,
                                                                      [DurableClient] IDurableOrchestrationClient client,
                                                                      string workflowName, int stepNumber, string globalKey)
        {

            MicroflowRun workflowRun = new()
            {
                OrchestratorInstanceId = Guid.NewGuid().ToString(),
                WorkflowName = workflowName,
                RunObject = new()
                {
                    RunId = Guid.NewGuid().ToString(),
                    StepNumber= stepNumber.ToString()
                }
            };

            if (string.IsNullOrWhiteSpace(globalKey))
            {
                workflowRun.RunObject.GlobalKey = Guid.NewGuid().ToString();
            }
            else
            {
                workflowRun.RunObject.GlobalKey = globalKey;
            }

            string instanceId = await client.StartNewAsync("RunWorkflowFromStep", null, workflowRun);

            HttpResponseMessage response = await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromSeconds(1));

            return new(HttpStatusCode.OK)
            {
                //Content = new StringContent(JsonSerializer.Serialize(step))
            };
        }


        /// <summary>
        /// Start a new workflow run
        /// </summary>
        /// <returns></returns>
        [Deterministic]
        [FunctionName("RunWorkflowFromStep")]
        public static async Task StartFromStep([OrchestrationTrigger] IDurableOrchestrationContext context)
                                                       //MicroflowRun workflowRun)
        {
            MicroflowRun workflowRun = context.GetInput<MicroflowRun>();

            // log start
            string logRowKey = TableHelper.GetTableLogRowKeyDescendingByDate(context.CurrentUtcDateTime, $"_{workflowRun.OrchestratorInstanceId}_fromStep_{workflowRun.RunObject.StepNumber}");

            //log.LogInformation($"Started orchestration with ID = '{context.InstanceId}', workflow = '{workflowRun.WorkflowName}'");

            await context.LogOrchestrationStartAsync(workflowRun, logRowKey);

            await context.CallSubOrchestratorAsync(CallNames.ExecuteStep, workflowRun);

            // log to table workflow completed
            Task logTask = context.LogOrchestrationEnd(workflowRun, logRowKey);

            context.SetMicroflowStateReady(workflowRun);

            await logTask;
            // done
        }

        /// <summary>
        /// Get a Microflow step config
        /// </summary>
        [FunctionName("GetStep")]
        public static async Task<HttpResponseMessage> GetMicroflowStep([HttpTrigger(AuthorizationLevel.Anonymous, "get",
                                                                      Route = MicroflowPath + "/GetStep/{workflowName}/{stepNumber}")] HttpRequestMessage req,
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
                                                                      Route = MicroflowPath + "/UpsertStep")] HttpRequestMessage req)
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
                                                                      Route = MicroflowPath + "/SetStepProperties/{workflowName}/{stepNumber}")] HttpRequestMessage req,
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

            foreach(KeyValuePair<string, object> kvp in kvpList)
            {
                System.Reflection.PropertyInfo prop = t.GetProperty(kvp.Key);

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
