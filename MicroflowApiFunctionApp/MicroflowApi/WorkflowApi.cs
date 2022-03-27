#if DEBUG || RELEASE || !DEBUG_NO_UPSERT && !DEBUG_NO_UPSERT_FLOWCONTROL && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_UPSERT && !RELEASE_NO_UPSERT_FLOWCONTROL && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using MicroflowShared;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace MicroflowApi
{
    public static class WorkflowApi
    {
        /// <summary>
        /// Delete orchestration history
        /// </summary>
        /// <param name="workflowName"></param>
        [FunctionName("PurgeInstanceHistory")]
        public static async Task<HttpResponseMessage> PurgeInstanceHistory([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = MicroflowModels.Constants.MicroflowVersion + "/PurgeInstanceHistory/{workflowName?}")]
                                                                HttpRequestMessage req,
                                                                [DurableClient] IDurableOrchestrationClient client,
                                                                string workflowName)
        {
            try
            {
                await client.PurgeInstanceHistoryAsync(workflowName);

                return new HttpResponseMessage(HttpStatusCode.OK);
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
        /// This must be called at least once before a workflow runs,
        /// this is to prevent multiple concurrent instances from writing step data at workflow run,
        /// call UpsertWorkflow when something changed in the workflow, but do not always call this when concurrent multiple workflows
        /// </summary>
        [FunctionName("UpsertWorkflow")]
        public static async Task<HttpResponseMessage> UpsertWorkflow([HttpTrigger(AuthorizationLevel.Anonymous, "post",
                                                                  Route = MicroflowModels.Constants.MicroflowVersion + "/UpsertWorkflow/{globalKey?}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string globalKey)
        {
            return await client.UpsertWorkflow(await req.Content.ReadAsStringAsync(), globalKey);
        }


        /// <summary>
        /// Returns the workflow Json that was saved with UpsertWorkflow
        /// </summary>
        [FunctionName("GetWorkflow")]
        public static async Task<string> GetWorkflowJson([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = MicroflowModels.Constants.MicroflowVersion + "/GetWorkflow/{workflowName}")] HttpRequestMessage req,
                                                           string workflowName)
        {
            return await WorkflowHelper.GetWorkflowJson(workflowName);
        }
    }
}
#endif