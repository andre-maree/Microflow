#if DEBUG || RELEASE || !DEBUG_NO_UPSERT && !DEBUG_NO_UPSERT_FLOWCONTROL && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_UPSERT && !RELEASE_NO_UPSERT_FLOWCONTROL && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUN
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using MicroflowShared;
using Microflow.Helpers;

namespace MicroflowApi
{
    public class WorkflowSplitCode
    {
        /// <summary>
        /// Inserts a workflow and starts it - skips workflow and global states, deletes, workflow save, create tables
        /// This will update the entire entity, so if its not the same steps then an unexpacted issue can occur
        /// </summary>
        [FunctionName("QuickInsertAndStartWorkflow")]
        public static async Task<HttpResponseMessage> QuickInsertAndStartWorkflow([HttpTrigger(AuthorizationLevel.Anonymous, "post",
                                                                  Route = MicroflowModels.Constants.MicroflowPath + "/QuickInsertAndStartWorkflow/{workflowNameVersion}/{instanceId?}/{globalKey?}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableOrchestrationClient client, string workflowNameVersion, string instanceId, string globalKey)
        {
            await client.QuickInsert(await req.Content.ReadAsStringAsync(), globalKey);

            return await client.StartWorkflow(req, instanceId, workflowNameVersion);
        }

        /// <summary>
        /// This must be called at least once before a workflow runs,
        /// this is to prevent multiple concurrent instances from writing step data at workflow run,
        /// call UpsertWorkflow when something changed in the workflow, but do not always call this when concurrent multiple workflows
        /// </summary>
        [FunctionName("UpsertWorkflow")]
        public static async Task<HttpResponseMessage> UpsertWorkflow([HttpTrigger(AuthorizationLevel.Anonymous, "post",
                                                                  Route = MicroflowModels.Constants.MicroflowPath + "/UpsertWorkflow/{globalKey?}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string globalKey)
        {
            return await client.UpsertWorkflow(await req.Content.ReadAsStringAsync(), globalKey);
        }


        /// <summary>
        /// Returns the workflow Json that was saved with UpsertWorkflow
        /// </summary>
        [FunctionName("GetWorkflow")]
        public static async Task<string> GetWorkflowJson([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = MicroflowModels.Constants.MicroflowPath + "/GetWorkflow/{workflowName}")] HttpRequestMessage req,
                                                           string workflowName)
        {
            return await WorkflowHelper.GetWorkflowJson(workflowName);
        }
    }
}
#endif