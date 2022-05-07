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
    public static class DynamicWebhook
    {
        /// <summary>
        /// For a webhook defined as "{webhook}", this can be changed to a non-catch-all like "myWebhook"
        /// </summary>
        [FunctionName("DynamicWebhook")]
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
        }
    }
}
