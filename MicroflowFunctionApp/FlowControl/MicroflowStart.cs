using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microflow.Helpers;
using System.Net;
using MicroflowModels;
using Azure;
using static MicroflowModels.Constants;

namespace Microflow.FlowControl
{
    /// <summary>
    /// UpsertWorkflow must be called to save workflow step meta data to table storage
    /// after this, Start can be called multiple times,
    /// if a change is made to the workflow, call UpsertWorkflow again to apply the changes
    /// </summary>
    public static class MicroflowStartFunctions
    {
        /// <summary>
        /// This is the entry point, workflow payload is in the http body
        /// </summary>
        /// <param name="instanceId">If an instanceId is passed in, it will run as a singleton, else it will run concurrently with each with a new instanceId</param>
        [FunctionName(CallNames.MicroflowStart)]
        public static async Task<HttpResponseMessage> MicroflowStart([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = MicroflowBase + "/Start/{workflowName}/{instanceId?}")]
                                                                HttpRequestMessage req,
                                                                [DurableClient] IDurableOrchestrationClient client,
                                                                string instanceId, string workflowName)
        {
            try
            {
                MicroflowRun workflowRun = MicroflowWorkflowHelper.CreateMicroflowRun(req, ref instanceId, workflowName);
                
                // start
                await client.StartNewAsync("MicroflowStartOrchestration", instanceId, workflowRun);

                return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromSeconds(1));

            }
            catch (RequestFailedException ex)
            {
                HttpResponseMessage resp = new(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(ex.Message + " - workflow in error state, call 'UpsertWorkflow' at least once before running a workflow.")
                };

                return resp;
            }
            catch (Exception e)
            {
                HttpResponseMessage resp = new(HttpStatusCode.InternalServerError)
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
        [FunctionName(CallNames.MicroflowStartOrchestration)]
        public static async Task MicroflowStartOrchestration([OrchestrationTrigger] IDurableOrchestrationContext context,
                                       ILogger inLog)
        {
            ILogger log = context.CreateReplaySafeLogger(inLog);

            // read workflowRun payload
            MicroflowRun workflowRun = context.GetInput<MicroflowRun>();

            try
            {
                var resp = context.MicroflowCheckAndWaitForReadyToRun(workflowRun.WorkflowName);

                if (!await resp)
                {
                    return;
                }

                await context.StartMicroflow(log, workflowRun);
            }
            catch (RequestFailedException e)
            {
                // log to table workflow completed
                LogErrorEntity errorEntity = new(workflowRun.WorkflowName,
                                                                Convert.ToInt32(workflowRun.RunObject.StepNumber),
                                                                e.Message,
                                                                workflowRun.RunObject.RunId);

                await context.CallActivityAsync(CallNames.LogError, errorEntity);
            }
            catch (Exception e)
            {
                // log to table workflow completed
                LogErrorEntity errorEntity = new(workflowRun.WorkflowName,
                                                                Convert.ToInt32(workflowRun.RunObject.StepNumber),
                                                                e.Message,
                                                                workflowRun.RunObject.RunId);

                await context.CallActivityAsync(CallNames.LogError, errorEntity);
            }
        }
    }
}