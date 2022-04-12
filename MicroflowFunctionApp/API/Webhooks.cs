using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microflow.Models;
using System.Collections.Generic;

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
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{orchestratorId}/{stepId}/{fail:bool?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient client, string webhook, int stepId, string orchestratorId, bool? fail)
        {
            return await client.GetWebhookResult(req, webhook, webhook, orchestratorId, fail);
        }

        /// <summary>
        /// For a webhook defined as {name}/{action}
        /// </summary>
        [FunctionName("WebhookWithAction")]
        public static async Task<HttpResponseMessage> WebhookWithAction(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{action}/{orchestratorId}/{stepId}/{fail:bool?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient client, string webhook, int stepId, string action, string orchestratorId, bool? fail)
        {
            return await client.GetWebhookResult(req, webhook, $"{webhook}/{action}", orchestratorId, fail);
        }

        /// <summary>
        /// For a webhook defined as {name}/{action}/{subaction}
        /// </summary>
        [FunctionName("WebhookWithActionAndSubAction")]
        public static async Task<HttpResponseMessage> WebhookWithActionAndSubAction(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{action}/{subaction}/{orchestratorId}/{stepId}/{fail:bool?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient client, [DurableClient] IDurableEntityClient client2, string webhook, int stepId, string action, string subaction, string orchestratorId, bool? fail)
        {
            return await client.GetWebhookResult(client2, req, webhook, $"{webhook}/{action}/{subaction}", orchestratorId, fail);
        }

        private static async Task<HttpResponseMessage> GetWebhookResult(this IDurableOrchestrationClient client,
                                                                        IDurableEntityClient client2,
                                                                        HttpRequestMessage req,
                                                                        string lookupName,
                                                                        string webhook,
                                                                        string orchestratorId,
                                                                        bool? fail,
                                                                        List<int> SubStepsToRun = null)
        {
            WebhookResult webhookResult = new()
            {
                StatusCode = !fail.HasValue || fail.Value == true ? 200 : 418
            };

            if (req.Method == HttpMethod.Post)
            {
                webhookResult.Content = await req.Content.ReadAsStringAsync();

                if (SubStepsToRun != null)
                {
                    webhookResult.SubStepsToRun = SubStepsToRun;
                }
                else
                {
                    EntityId entId = new(lookupName, webhook);

                    EntityStateResponse<string> flowInfo = await client2.ReadEntityStateAsync<string>(entId);

                    SubStepsToRun = new List<int>() { flowInfo.EntityState.Split(',') };
                }
            }
            
            await client.RaiseEventAsync(orchestratorId, $"{webhook}", webhookResult);

            return new(HttpStatusCode.OK);
        }
    }
}
