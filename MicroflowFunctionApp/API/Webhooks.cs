using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microflow.Models;
using MicroflowModels.Helpers;
using MicroflowModels;

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
        [FunctionName("DynaWebhook")]
        public static async Task<HttpResponseMessage> DynaWebhook(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/webhook/{webhookId}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient,
        string webhookId)
        {
            MicroflowHttpResponseBase webhookResult = new()
            {
                Content = await req.Content.ReadAsStringAsync(),
                HttpResponseStatusCode = 200,
                Success = true
            };

            string key = "webhook@" + webhookId;
            await orchClient.RaiseEventAsync(key, key, webhookResult);

            return new(HttpStatusCode.OK);
            //return await orchClient.GetWebhookResult(req, webhook, string.Empty, string.Empty, orchestratorId, stepId);
        }

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
            var webHooksTask = TableHelper.GetWebhookSubSteps(webhookBase, stepId);

            MicroflowHttpResponse webhookResult = new()
            {
                Success = true,
                HttpResponseStatusCode = 200
            };

            string action;

            if (!string.IsNullOrEmpty(webhookSubAction))
            {
                action = $"{webhookBase}/{webhookAction}/{webhookSubAction}";
            }
            else if (!string.IsNullOrEmpty(webhookAction))
            {
                action = $"{webhookBase}/{webhookAction}";
            }
            else
            {
                action = $"{webhookBase}";
            }

            var subStepsMapping = await webHooksTask;

            if (subStepsMapping.Count > 0)
            {
                var hook = subStepsMapping.Find(h => h.WebhookAction.Equals(action));

                if (hook != null)
                {
                    webhookResult.SubStepsToRun = hook.SubStepsToRunForAction;
                }
                else
                {
                    return new(HttpStatusCode.BadRequest);
                }
            }

            await client.RaiseEventAsync(orchestratorId, orchestratorId, webhookResult);

            return new(HttpStatusCode.OK);
        }
    }
}
