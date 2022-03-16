using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microflow.Helpers;
using System.Net;
using Microflow.Models;
using static Microflow.Helpers.Constants;
using Azure;

namespace Microflow.FlowControl
{
    /// <summary>
    /// "Microflow_InsertOrUpdateworkflow" must be called to save workflow step meta data to table storage
    /// after this, "Microflow_HttpStart" can be called multiple times,
    /// if a change is made to the workflow, call "Microflow_InsertOrUpdateworkflow" again to apply the changes
    /// </summary>
    public static class MicroflowStart
    {
        /// <summary>
        /// This is the entry point, workflow payload is in the http body
        /// </summary>
        /// <param name="instanceId">If an instanceId is passed in, it will run as a singleton, else it will run concurrently with each with a new instanceId</param>
        [FunctionName("Microflow_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "start/{workflowName}/{instanceId?}")]
                                                                HttpRequestMessage req,
                                                                [DurableClient] IDurableOrchestrationClient client,
                                                                string instanceId, string workflowName)
        {
            try
            {
                MicroflowRun workflowRun = MicroflowWorkflowHelper.CreateMicroflowRun(req, ref instanceId, workflowName);

                // start
                await client.StartNewAsync("Start", instanceId, workflowRun);

                return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromSeconds(1));

            }
            catch (RequestFailedException ex)
            {
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(ex.Message + " - workflow in error state, call 'InsertOrUpdateworkflow' at least once before running a workflow.")
                };

                return resp;
            }
            catch (Exception e)
            {
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(e.Message)
                };

                return resp;
            }
        }

        /// <summary>
        /// This is called from Microflow_HttpStart, it does the looping and calls the ExecuteStep sub orchestration passing in the top step
        /// </summary>
        /// <returns></returns>
        [Deterministic]
        [FunctionName("Start")]
        public static async Task Start([OrchestrationTrigger] IDurableOrchestrationContext context,
                                       ILogger inLog)
        {
            ILogger log = context.CreateReplaySafeLogger(inLog);

            // read workflowRun payload
            MicroflowRun workflowRun = context.GetInput<MicroflowRun>();

            try
            {
                var resp = context.CheckAndWaitForReadyToRun(workflowRun.WorkflowName, log);

                if (!await context.CheckAndWaitForReadyToRun(workflowRun.WorkflowName, log))
                {
                    return;
                }

                await context.StartMicroflow(log, workflowRun);
            }
            catch (RequestFailedException e)
            {
                // log to table workflow completed
                LogErrorEntity errorEntity = new LogErrorEntity(workflowRun.WorkflowName,
                                                                Convert.ToInt32(workflowRun.RunObject.StepNumber),
                                                                e.Message,
                                                                workflowRun.RunObject.RunId);

                await context.CallActivityAsync(CallNames.LogError, errorEntity);
            }
            catch (Exception e)
            {
                // log to table workflow completed
                LogErrorEntity errorEntity = new LogErrorEntity(workflowRun.WorkflowName,
                                                                Convert.ToInt32(workflowRun.RunObject.StepNumber),
                                                                e.Message,
                                                                workflowRun.RunObject.RunId);

                await context.CallActivityAsync(CallNames.LogError, errorEntity);
            }
        }

        /// <summary>
        /// This must be called at least once before a workflow runs,
        /// this is to prevent multiple concurrent instances from writing step data at workflow run,
        /// call Microflow InsertOrUpdateworkflow when something changed in the workflow, but do not always call this when concurrent multiple workflows
        /// </summary>
        [FunctionName("UpsertWorkflow")]
        public static async Task<HttpResponseMessage> SaveWorflow([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "UpsertWorkflow/{globalKey?}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string globalKey)
        {
            return await client.UpsertWorkflow(await req.Content.ReadAsStringAsync(), globalKey);
        }
    }
}