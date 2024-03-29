using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using MicroflowModels.Helpers;
using MicroflowModels;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

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
        Route = Constants.MicroflowPath + "/Webhooks/{webhookId}/{action?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient orchClient,
        string webhookId, string action)
            => await orchClient.ProcessWebhook(webhookId, action, req.RequestUri);

        private static async Task<HttpResponseMessage> ProcessWebhook(this IDurableOrchestrationClient client,
                                                                        string webhookId,
                                                                        string action,
                                                                        Uri location)
        {
            Webhook webhook = await TableHelper.GetWebhook(webhookId);

            if (webhook == null)
            {
                return new(HttpStatusCode.NotFound);
            }

            try
            {
                // no action
                if (string.IsNullOrWhiteSpace(action))
                {
                    if (!string.IsNullOrWhiteSpace(webhook.WebhookSubStepsMapping))
                    {
                        return new(HttpStatusCode.NotFound);
                    }

                    await client.RaiseEventAsync(webhookId, webhookId, new MicroflowHttpResponse()
                    {
                        Success = true,
                        HttpResponseStatusCode = 200
                    });

                    return new(HttpStatusCode.OK);
                }

                // with action
                if (string.IsNullOrWhiteSpace(webhook.WebhookSubStepsMapping))
                {
                    return new(HttpStatusCode.NotFound);
                }

                List<SubStepsMappingForActions> webhookSubStepsMapping = JsonSerializer.Deserialize<List<SubStepsMappingForActions>>(webhook.WebhookSubStepsMapping);

                SubStepsMappingForActions hook = webhookSubStepsMapping.FirstOrDefault(h => h.WebhookAction.Equals(action, StringComparison.OrdinalIgnoreCase));

                MicroflowHttpResponse webhookResult;

                if (hook != null)
                {
                    webhookResult = new()
                    {
                        Success = true,
                        HttpResponseStatusCode = 200,
                        SubStepsToRun = hook.SubStepsToRunForAction,
                        Action = action
                    };
                }
                else
                {
                    return new(HttpStatusCode.NotFound);
                }

                await client.RaiseEventAsync(webhookId, webhookId, webhookResult);

                return new(HttpStatusCode.OK);
            }
            catch (ArgumentException)
            {
                try
                {
                    DurableOrchestrationStatus statusCheck = await client.GetStatusAsync("callout" + webhookId);

                    if (statusCheck == null)
                    {
                        return new(HttpStatusCode.NotFound);
                    }

                    // unliky but possible that the event have not yet been created
                    HttpResponseMessage response = new(HttpStatusCode.Accepted);
                    response.Headers.Location = location;

                    return response;
                }
                catch
                {
                    return new(HttpStatusCode.InternalServerError);
                }
            }
            catch
            {
                return new(HttpStatusCode.InternalServerError);
            }
        }
    }
}
