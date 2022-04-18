using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microflow.Models;
using System.Collections.Generic;

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
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{orchestratorId}/{stepId}/{fail:bool?}/{lookupSubStepsToRun:bool?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient, [DurableClient] IDurableEntityClient entClient,
        string webhook, int stepId, string orchestratorId, bool? fail, bool? lookupSubStepsToRun)
            => await orchClient.GetWebhookResult(entClient, req, webhook, string.Empty, string.Empty, orchestratorId, fail, lookupSubStepsToRun);

        ///// <summary>
        ///// For a webhook defined as {name}/{action}, this can be changed to a non-catch-all like "myWebhook/myAction"
        ///// </summary>
        [FunctionName("WebhookWithAction")]
        public static async Task<HttpResponseMessage> WebhookWithAction(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{action}/{orchestratorId}/{stepId}/{fail:bool?}/{lookupSubStepsToRun:bool?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient, [DurableClient] IDurableEntityClient entClient,
        string webhook, int stepId, string action, string orchestratorId, bool? fail, bool? lookupSubStepsToRun)
            => await orchClient.GetWebhookResult(entClient, req, webhook, $"{action}", string.Empty, orchestratorId, fail, lookupSubStepsToRun);

        /// <summary>
        /// For a webhook defined as {name}/{action}/{subaction}, this can be changed to a non-catch-all like "myWebhook/myAction/mySubAction"
        /// </summary>
        [FunctionName("WebhookWithActionAndSubAction")]
        public static async Task<HttpResponseMessage> WebhookWithActionAndSubAction(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{action}/{subaction}/{orchestratorId}/{stepId}/{fail:bool?}/{lookupSubStepsToRun:bool?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient, [DurableClient] IDurableEntityClient entClient,
        string webhook, int stepId, string action, string subaction, string orchestratorId, bool? fail, bool? lookupSubStepsToRun)
            => await orchClient.GetWebhookResult(entClient, req, webhook, action, subaction, orchestratorId, fail, lookupSubStepsToRun);

        private static async Task<HttpResponseMessage> GetWebhookResult(this IDurableOrchestrationClient client,
                                                                        IDurableEntityClient entClient,
                                                                        HttpRequestMessage req,
                                                                        string wehookBase,
                                                                        string webhookAction,
                                                                        string webhookSubAction,
                                                                        string orchestratorId,
                                                                        bool? fail,
                                                                        bool? lookupSubStepsToRun)
        {
            WebhookResult webhookResult = new()
            {
                StatusCode = !fail.HasValue || fail.Value == true ? 200 : 418                
            };

            //string entkey;
            ////string webhookKey;

            if (!string.IsNullOrEmpty(webhookSubAction))
            {
                webhookResult.ActionPath = $"{wehookBase}/{webhookAction}/{webhookSubAction}";
                //entkey = $"{wehookBase}@{webhookAction}@{webhookSubAction}";
                //webhookKey = $"{wehookBase}/{webhookAction}/{webhookSubAction}";
            }
            else if (!string.IsNullOrEmpty(webhookAction))
            {
                webhookResult.ActionPath = $"{wehookBase}/{webhookAction}";
                //entkey = $"{wehookBase}@{webhookAction}";
                //webhookKey = $"{wehookBase}/{webhookAction}";
            }
            else
            {
                webhookResult.ActionPath = $"{wehookBase}";
                //entkey = $"{wehookBase}";
                //webhookKey = $"{wehookBase}";
            }

            //if (req.Method == HttpMethod.Post)
            //{
            //    webhookResult.Content = await req.Content.ReadAsStringAsync();

            //    // lookup substeps to run
            //    if (lookupSubStepsToRun.HasValue)
            //    {
            //        if (lookupSubStepsToRun.Value)
            //        {
            //            //EntityId entId = new("StepFlowState", entkey);

            //            //EntityStateResponse<List<int>> flowInfo = await entClient.ReadEntityStateAsync<List<int>>(entId);

            //            //if (flowInfo.EntityExists)
            //            //{
            //            //    webhookResult.SubStepsToRun = flowInfo.EntityState;
            //            //}
            //        }
            //    }
            //}

            await client.RaiseEventAsync(orchestratorId, orchestratorId, webhookResult);

            return new(HttpStatusCode.OK);
        }
    }
}
