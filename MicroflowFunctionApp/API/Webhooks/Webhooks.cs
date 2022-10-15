using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using MicroflowModels.Helpers;
using MicroflowModels;
using System;

namespace Microflow.Webhooks
{
    /// <summary>
    /// This is a generic webhooks implementation with /webhooks/{webhookId}/{action}
    /// Substeps to execute can be set with the step`s WebhookSubStepsMapping property, accociated to the action
    /// </summary>
    public static class Webhooks
    {
        ///// <summary>
        ///// For a webhook defined as {webhookId}/{action}
        ///// </summary>
        [FunctionName("Webhook")]
        public static async Task<HttpResponseMessage> WebhookWithAction(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = Constants.MicroflowPath + "/webhooks/{webhookId}/{action?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient,
        string webhookId, string action)
            => await orchClient.ProcessWebhook(webhookId, action);

        private static async Task<HttpResponseMessage> ProcessWebhook(this IDurableOrchestrationClient client,
                                                                        string webhookId,
                                                                        string action)
        {
            try
            {
                MicroflowHttpResponse webhookResult = new()
                {
                    Success = true,
                    HttpResponseStatusCode = 200
                };

                if (string.IsNullOrEmpty(action))
                {
                    await client.RaiseEventAsync(webhookId, webhookId, webhookResult);

                    return new(HttpStatusCode.OK);
                }

                var webHooksTask = TableHelper.GetWebhookSubSteps(webhookId);

                var subStepsMapping = await webHooksTask;

                if (subStepsMapping != null && subStepsMapping.Count > 0)
                {
                    var hook = subStepsMapping.Find(h => h.WebhookAction.Equals(action, System.StringComparison.OrdinalIgnoreCase));

                    if (hook != null)
                    {
                        webhookResult.SubStepsToRun = hook.SubStepsToRunForAction;
                        webhookResult.Action = action;
                    }
                    else
                    {
                        return new(HttpStatusCode.BadRequest);
                    }
                }

                await client.RaiseEventAsync(webhookId, webhookId, webhookResult);

                return new(HttpStatusCode.OK);
            }
            catch
            {
                return new(HttpStatusCode.InternalServerError);
            }
        }
    }
}
