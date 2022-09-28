using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using MicroflowModels.Helpers;
using MicroflowModels;

namespace Microflow.Webhooks
{
    /// <summary>
    /// This is a generic webhooks implementation with /webhooks/{webhookId}/{stepId}/{action}
    /// Substeps to execute can be set with the step`s WebhookSubStepsMapping property, accociated to the action
    /// </summary>
    public static class Webhooks
    {
        /// <summary>
        /// For a webhook defined as "{webhookId}/{stepId}"
        /// </summary>
        [FunctionName("Webhook")]
        public static async Task<HttpResponseMessage> Webhook(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = Constants.MicroflowPath + "/webhooks/{webhookId}/{stepId}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient,
        string webhookId, string stepId)
            => await orchClient.ProcessWebhook(req, webhookId, string.Empty, stepId);

        ///// <summary>
        ///// For a webhook defined as {webhookId}/{stepId}/{action}
        ///// </summary>
        [FunctionName("WebhookWithAction")]
        public static async Task<HttpResponseMessage> WebhookWithAction(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = Constants.MicroflowPath + "/webhooks/{webhookId}/{stepId}/{action}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient,
        string stepId, string webhookId, string action)
            => await orchClient.ProcessWebhook(req, webhookId, action, stepId);

        private static async Task<HttpResponseMessage> ProcessWebhook(this IDurableOrchestrationClient client,
                                                                        HttpRequestMessage req,
                                                                        string webhookId,
                                                                        string action,
                                                                        string stepId)
        {
            var webHooksTask = TableHelper.GetWebhookSubSteps(GetWorkFlowNameAndVersion(webhookId), stepId);

            MicroflowHttpResponse webhookResult = new()
            {
                Success = true,
                HttpResponseStatusCode = 200
            };

            var subStepsMapping = await webHooksTask;

            if (subStepsMapping != null && subStepsMapping.Count > 0)
            {
                var hook = subStepsMapping.Find(h => h.WebhookAction.Equals(action, System.StringComparison.OrdinalIgnoreCase));

                if (hook != null)
                {
                    webhookResult.SubStepsToRun = hook.SubStepsToRunForAction;
                }
                else
                {
                    return new(HttpStatusCode.BadRequest);
                }
            }

            string orchId = $"{webhookId}@{stepId}";

            await client.RaiseEventAsync(orchId, orchId, webhookResult);

            return new(HttpStatusCode.OK);
        }

        private static string GetWorkFlowNameAndVersion(string webhookAction)
        {
            bool found = false;

            for (int i = 0; i < webhookAction.Length; i++)
            {
                char c = webhookAction[i];

                if (c == '@')
                {
                    if (found)
                        return webhookAction.Substring(0, i);

                    found = true;
                }
            }

            return string.Empty;
        }
    }
}
