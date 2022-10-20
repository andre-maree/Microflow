using Azure;
using MicroflowModels;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System;
using System.Collections.Specialized;
using static MicroflowModels.Constants;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;

namespace Microflow.Helpers
{
    public static class MicroflowStartupHelper
    {
        public static async Task<HttpResponseMessage> StartWorkflow(this IDurableOrchestrationClient client, HttpRequestMessage req, string instanceId, string workflowName)
        {
            try
            {
                MicroflowRun workflowRun = MicroflowWorkflowHelper.CreateMicroflowRun(req, ref instanceId, workflowName);

                // start
                await client.StartNewAsync(CallNames.MicroflowStartOrchestration, instanceId, workflowRun);

                HttpResponseMessage response = await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromSeconds(1));

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    response.Content = new StringContent(instanceId);
                }

                return response;

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
        /// Create a new workflowRun for stratup, set GlobalKey
        /// </summary>
        public static MicroflowRun CreateStartupRun(NameValueCollection data,
                                                         ref string instanceId,
                                                         string workflowName)
        {
            var input = new
            {
                Loop = Convert.ToInt32(data["loop"]),
                GlobalKey = data["globalkey"]
            };

            // create a workflow run
            MicroflowRun workflowRun = new()
            {
                WorkflowName = workflowName,
                Loop = input.Loop != 0
                ? input.Loop
                : 1
            };

            // create a new run object
            RunObject runObj = new();
            workflowRun.RunObject = runObj;

            // instanceId is set/singleton
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                // globalKey is set
                if (!string.IsNullOrWhiteSpace(input.GlobalKey))
                {
                    runObj.GlobalKey = input.GlobalKey;
                }
                else
                {
                    runObj.GlobalKey = Guid.NewGuid().ToString();
                }
            }
            // instanceId is not set/multiple concurrent instances
            else
            {
                instanceId = Guid.NewGuid().ToString();
                // globalKey is set
                if (!string.IsNullOrWhiteSpace(input.GlobalKey))
                {
                    runObj.GlobalKey = input.GlobalKey;
                }
                else
                {
                    runObj.GlobalKey = instanceId;
                }
            }

            //workflowRun.RunObject.StepNumber = "-1";
            workflowRun.OrchestratorInstanceId = instanceId;

            return workflowRun;
        }
    }
}
