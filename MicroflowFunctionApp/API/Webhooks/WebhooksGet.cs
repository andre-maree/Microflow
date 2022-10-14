using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using MicroflowModels;
using Microflow.Helpers;

namespace Microflow.Webhooks
{
    /// <summary>
    /// This is a generic webhooks implementation with /webhooks/{webhookId}/{stepId}/{action}
    /// Substeps to execute can be set with the step`s WebhookSubStepsMapping property, accociated to the action
    /// </summary>
    public static class WebhooksGet
    {
        /// <summary>
        /// For a webhook defined as "{webhookId}/{stepId}"
        /// </summary>
        [FunctionName("GetWebhooks")]
        public static async Task<HttpResponseMessage> GetWebhooks(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = Constants.MicroflowPath + "/GetWebhooks/{workflowName}/{webhookId}/{stepNumber}/{instanceGuid?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient,
        string workflowName, string webhookId, int stepNumber, string instanceGuid = "")
        {
            var li = await MicroflowTableHelper.GetWebhooks(workflowName, webhookId, stepNumber, instanceGuid);

            return null;
        }

        private static async Task GetWebhooks()
        {
            await Task.Delay(3);
        }

    }
}
