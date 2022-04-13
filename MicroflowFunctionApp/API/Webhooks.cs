using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microflow.Models;
using System.Collections.Generic;
using System.Linq;

namespace Microflow.Webhook
{
    /// <summary>
    /// These are custom webhooks that can be defined here within Microflow, or extracted into its own function app (response proxies app)
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
            => await orchClient.GetWebhookResult(entClient, req, webhook, webhook, orchestratorId, fail, lookupSubStepsToRun);

        ///// <summary>
        ///// For a webhook defined as {name}/{action}, this can be changed to a non-catch-all like "myWebhook/myAction"
        ///// </summary>
        [FunctionName("WebhookWithAction")]
        public static async Task<HttpResponseMessage> WebhookWithAction(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{action}/{orchestratorId}/{stepId}/{fail:bool?}/{lookupSubStepsToRun:bool?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient, [DurableClient] IDurableEntityClient entClient,
        string webhook, int stepId, string action, string orchestratorId, bool? fail, bool? lookupSubStepsToRun)
            => await orchClient.GetWebhookResult(entClient, req, webhook, $"{webhook}/{action}", orchestratorId, fail, lookupSubStepsToRun);

        /// <summary>
        /// For a webhook defined as {name}/{action}/{subaction}, this can be changed to a non-catch-all like "myWebhook/myAction/mySubAction"
        /// </summary>
        [FunctionName("WebhookWithActionAndSubAction")]
        public static async Task<HttpResponseMessage> WebhookWithActionAndSubAction(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{action}/{subaction}/{orchestratorId}/{stepId}/{fail:bool?}/{lookupSubStepsToRun:bool?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient, [DurableClient] IDurableEntityClient entClient,
        string webhook, int stepId, string action, string subaction, string orchestratorId, bool? fail, bool? lookupSubStepsToRun)
            => await orchClient.GetWebhookResult(entClient, req, webhook, $"{action}/{subaction}", orchestratorId, fail, lookupSubStepsToRun);

        private static async Task<HttpResponseMessage> GetWebhookResult(this IDurableOrchestrationClient client,
                                                                        IDurableEntityClient entClient,
                                                                        HttpRequestMessage req,
                                                                        string wehookBase,
                                                                        string webhookAction,
                                                                        string orchestratorId,
                                                                        bool? fail,
                                                                        bool? lookupSubStepsToRun)
        {
            WebhookResult webhookResult = new()
            {
                StatusCode = !fail.HasValue || fail.Value == true ? 200 : 418
            };

            if (req.Method == HttpMethod.Post)
            {
                webhookResult.Content = await req.Content.ReadAsStringAsync();

                //TODO: lookup substeps to run
                if (lookupSubStepsToRun.HasValue)
                {
                    if (lookupSubStepsToRun.Value)
                    {
                        EntityId entId = new("StepFlowInfo", wehookBase);

                        EntityStateResponse<List<int>> flowInfo = await entClient.ReadEntityStateAsync<List<int>>(entId);

                        if (flowInfo.EntityExists)
                        {
                            webhookResult.SubStepsToRun = flowInfo.EntityState;
                        }
                    }
                    //else // try get it from the post data as 1,2,3
                    //{
                    //    List<int> arr = new();
                    //    bool? doSubSteps = null;

                    //    foreach (string s in webhookResult.Content.Split(',', System.StringSplitOptions.RemoveEmptyEntries))
                    //    {
                    //        if (int.TryParse(s, out int i))
                    //        {
                    //            arr.Add(i);
                    //            doSubSteps = true;
                    //        }
                    //        else
                    //        {
                    //            doSubSteps = false;
                    //            break;
                    //        }
                    //    }

                    //    if (doSubSteps.HasValue && doSubSteps.Value)
                    //    {
                    //        webhookResult.SubStepsToRun = arr.ToList();
                    //    }
                    //}
                }
            }

            await client.RaiseEventAsync(orchestratorId, $"{wehookBase}/{webhookAction}", webhookResult);

            return new(HttpStatusCode.OK);
        }
    }
}
