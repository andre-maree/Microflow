#if !DEBUGSPLITMODE
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using MicroflowShared;

namespace Microflow.SplitMode
{
    public class WorkflowSplitCode
    { 
        /// <summary>
        /// This must be called at least once before a workflow runs,
        /// this is to prevent multiple concurrent instances from writing step data at workflow run,
        /// call Microflow InsertOrUpdateworkflow when something changed in the workflow, but do not always call this when concurrent multiple workflows
        /// </summary>
        [FunctionName("UpsertWorkflow")]
        public static async Task<HttpResponseMessage> UpsertWorkflow([HttpTrigger(AuthorizationLevel.Anonymous, "post",
                                                                  Route = "UpsertWorkflow/{globalKey?}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string globalKey)
        {
            return await client.UpsertWorkflow(await req.Content.ReadAsStringAsync(), globalKey);
        }

        [FunctionName("GetWorkflowJson")]
        public static async Task<string> GetWorkflowJson([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetWorkflowJson/{workflowName}")] HttpRequestMessage req,
                                                           string workflowName)
        {
            return await SharedTableHelper.GetWorkflowJson(workflowName);
        }
    }
}

#endif