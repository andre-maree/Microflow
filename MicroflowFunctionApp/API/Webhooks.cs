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
    /// These are custom webhooks that can be defined here withing Microflow, or 
    /// </summary>
    public static class Webhooks
    {
        /// <summary>
        /// For a webhook defined as {webhook}
        /// </summary>
        [FunctionName("Webhook")]
        public static async Task<HttpResponseMessage> Webhook(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{orchestratorId}/{stepId}/{fail:bool?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient client, string webhook, int stepId, string orchestratorId, bool? fail)
        {
            return await client.GetWebhookResult(req, webhook, string.Empty, orchestratorId, fail);
        }

        /// <summary>
        /// For a webhook defined as {name}/{action}
        /// </summary>
        [FunctionName("WebhookWithAction")]// for web hook
        public static async Task<HttpResponseMessage> WebhookWithAction(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/{webhook}/{action}/{orchestratorId}/{stepId}/{fail:bool?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient client, string webhook, int stepId, string action, string orchestratorId, bool? fail)
        {
            return await client.GetWebhookResult(req, $"{webhook}/{action}", action, orchestratorId, fail);
        }

        /// <summary>
        /// For a webhook defined as {webhook}
        /// </summary>
        [FunctionName("WebhookSubStepSelector")]
        public static async Task<HttpResponseMessage> WebhookSubStepSelector(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
        Route = "/" + MicroflowModels.Constants.MicroflowPath + "/WebhookSubStepSelector/{orchestratorId}/{stepId}/{fail:bool?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient client, int stepId, string orchestratorId, bool? fail)
        {
            return await client.GetWebhookResult(req, "WebhookSubStepSelector", string.Empty, orchestratorId, fail, new List<int>() {3});
        }

        private static async Task<HttpResponseMessage> GetWebhookResult(this IDurableOrchestrationClient client,
                                                                        HttpRequestMessage req,
                                                                        string webhook,
                                                                        string action,
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
            }

            //if(SubStepsToRun != null && SubStepsToRun.Length > 0)
            //{
                webhookResult.SubStepsToRun = SubStepsToRun;
            //}

            await client.RaiseEventAsync(orchestratorId, $"{webhook}", webhookResult);

            return new(HttpStatusCode.OK);
        }
    }
}
