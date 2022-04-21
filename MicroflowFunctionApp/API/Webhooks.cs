using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microflow.Models;
using MicroflowModels.Helpers;

namespace Microflow.Webhooks
{
    /// <summary>
    /// These are custom webhooks that can be defined here within Microflow, or extracted into its own function app (response proxies app).
    /// These catch-all webhooks are able to look up which sub steps should execute. Call the Microflow Api "StepFlowControl/{wehookBase}/{webhookAction}/{webhookSubAction}/{stepList}"
    /// to set the sub steps list for the webhook.
    /// </summary>
    public static class Webhooks
    {
        /// <summary>
        /// For a webhook defined as "{webhook}", this can be changed to a non-catch-all like "myWebhook"
        /// </summary>
        [FunctionName("Webhook")]
        public static async Task<HttpResponseMessage> Webhook(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{orchestratorId}/{stepId}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient,
        string webhook, string stepId, string orchestratorId)
            => await orchClient.GetWebhookResult(req, webhook, string.Empty, string.Empty, orchestratorId, stepId);

        ///// <summary>
        ///// For a webhook defined as {name}/{action}, this can be changed to a non-catch-all like "myWebhook/myAction"
        ///// </summary>
        [FunctionName("WebhookWithAction")]
        public static async Task<HttpResponseMessage> WebhookWithAction(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{action}/{orchestratorId}/{stepId}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient,
        string webhook, string stepId, string action, string orchestratorId)
            => await orchClient.GetWebhookResult(req, webhook, $"{action}", string.Empty, orchestratorId, stepId);

        /// <summary>
        /// For a webhook defined as {name}/{action}/{subaction}, this can be changed to a non-catch-all like "myWebhook/myAction/mySubAction"
        /// </summary>
        [FunctionName("WebhookWithActionAndSubAction")]
        public static async Task<HttpResponseMessage> WebhookWithActionAndSubAction(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{action}/{subaction}/{orchestratorId}/{stepId}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient,
        string webhook, string stepId, string action, string subaction, string orchestratorId)
            => await orchClient.GetWebhookResult(req, webhook, action, subaction, orchestratorId, stepId);

        private static async Task<HttpResponseMessage> GetWebhookResult(this IDurableOrchestrationClient client,
                                                                        HttpRequestMessage req,
                                                                        string webhookBase,
                                                                        string webhookAction,
                                                                        string webhookSubAction,
                                                                        string orchestratorId,
                                                                        string stepId)
        {
            var webHooksTask = TableHelper.GetWebhooksForStep(webhookBase, stepId);

            WebhookResult webhookResult = new()
            {
                StatusCode = 200                
            };

            if (!string.IsNullOrEmpty(webhookSubAction))
            {
                webhookResult.ActionPath = $"{webhookBase}/{webhookAction}/{webhookSubAction}";
            }
            else if (!string.IsNullOrEmpty(webhookAction))
            {
                webhookResult.ActionPath = $"{webhookBase}/{webhookAction}";
            }
            else
            {
                webhookResult.ActionPath = $"{webhookBase}";
            }

            MicroflowModels.Webhook webHooks = await webHooksTask;

            var hook = webHooks.SubStepsMapping.Find(h => h.WebhookAction.Equals(webhookResult.ActionPath));

            if (hook != null)
            {
                webhookResult.SubStepsToRun = hook.SubStepsToRunForAction;
                
                await client.RaiseEventAsync(orchestratorId, orchestratorId, webhookResult);

                return new(HttpStatusCode.OK);
            }
            else
            {
                return new(HttpStatusCode.BadRequest);
            }
        }
    }
}
