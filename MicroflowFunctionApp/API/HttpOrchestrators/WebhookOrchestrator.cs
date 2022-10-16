using System;
using System.Threading.Tasks;
using Microflow.MicroflowTableModels;
using MicroflowModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using static MicroflowModels.Constants;

namespace Microflow.HttpOrchestrators
{
    public static class MicroflowHttpCallWithWebhook
    {
        /// <summary>
        /// Does the call out and then waits for the webhook
        /// </summary>
        [Deterministic]
        [FunctionName(CallNames.WebhookOrchestrator)]
        public static async Task<MicroflowHttpResponse> WebhookOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context,
                                                                             ILogger inLog)
        {
            ILogger log = context.CreateReplaySafeLogger(inLog);

            HttpCall httpCall = context.GetInput<HttpCall>();

            #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
            //////////////////////////////////////
            bool doneAdd = false;
            bool doneSubtract = false;
            EntityId countId = new(MicroflowEntities.StepCount, httpCall.PartitionKey + httpCall.RowKey);
            //////////////////////////////////////
#endif
            #endregion

            try
            {
                #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
                ////////////////////////////////////////////////
                // set the per step in-progress count to count+1
                context.SignalEntity(countId, MicroflowEntityKeys.Add);
                doneAdd = true;
                ////////////////////////////////////////////////
#endif
                #endregion

                string webhookRowKey = $"{httpCall.RowKey}~{httpCall.WebhookId}~{context.InstanceId}";

                await LogWebhookCreate(context, httpCall.PartitionKey, webhookRowKey, httpCall.RunId);

                MicroflowHttpResponse microflowWebhookResponse = null;

                // TODO: always use https
                //if (httpCall.WebhookSubStepsMapping != null && httpCall.WebhookSubStepsMapping.Length > 0)
                //{
                log.LogCritical($"Waiting for webhook: {CallNames.BaseUrl}/webhooks/{context.InstanceId}/" + "{action?}");

                // wait for the external event, set the timeout
                microflowWebhookResponse = await context.WaitForExternalEvent<MicroflowHttpResponse>(context.InstanceId,
                                                                                                       TimeSpan.FromSeconds(httpCall.WebhookTimeoutSeconds));
                //}
                //else
                //{
                //    log.LogCritical($"Waiting for webhook: {CallNames.BaseUrl}/webhooks/{context.InstanceId}");

                //    // wait for the external event, set the timeout
                //    microflowWebhookResponse = await context.WaitForExternalEvent<MicroflowHttpResponse>(context.InstanceId,
                //                                                                                           TimeSpan.FromSeconds(httpCall.WebhookTimeoutSeconds));
                //}

                #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
                ////////////////////////////////////////////////
                // set the per step in-progress count to count-1
                context.SignalEntity(countId, MicroflowEntityKeys.Subtract);
                doneSubtract = true;
                ////////////////////////////////////////////////
#endif
                #endregion

                // check for action failed
                if (microflowWebhookResponse.HttpResponseStatusCode >= 200 && microflowWebhookResponse.HttpResponseStatusCode < 300)
                {
                    await LogWebhookAction(context, httpCall.PartitionKey, webhookRowKey, microflowWebhookResponse.Action);

                    log.LogWarning($"Step {httpCall.RowKey} webhook {context.InstanceId} successful at {context.CurrentUtcDateTime:HH:mm:ss}");

                    return microflowWebhookResponse;
                }
                else
                {
                    return new MicroflowHttpResponse()
                    {
                        CalloutOrWebhook = CalloutOrWebhook.Webhook,
                        Success = false,
                        HttpResponseStatusCode = microflowWebhookResponse.HttpResponseStatusCode,
                        Content = $"Webhook action {context.InstanceId} falied"
                    };
                }
            }
            catch (TimeoutException tex)
            {
                throw tex;
            }
            catch (Exception e)
            {
                if (!httpCall.StopOnWebhookTimeout)
                {
                    return new MicroflowHttpResponse()
                    {
                        CalloutOrWebhook = CalloutOrWebhook.Webhook,
                        Success = false,
                        HttpResponseStatusCode = -500,
                        Content = $"Webhook action failed - " + e.Message
                    };
                }

                throw;
            }

            #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
            ////////////////////////////////////////////////
            finally
            {
                if (doneAdd && !doneSubtract)
                {
                    // set the per step in-progress count to count-1
                    context.SignalEntity(countId, MicroflowEntityKeys.Subtract);
                }
            }
            ////////////////////////////////////////////////
#endif
            #endregion
        }

        /// <summary>
        /// Log the start of the webhook
        /// </summary>
        [Deterministic]
        private static async Task LogWebhookCreate(IDurableOrchestrationContext context, string partitionKey, string rowKey, string runId)
        {
            await context.CallActivityAsync(
                CallNames.LogWebhook,
                new LogWebhookEntity(true,
                                  partitionKey,
                                  rowKey,
                                  context.CurrentUtcDateTime,
                                  runId)
            );
        }

        /// <summary>
        /// Log the webhook action
        /// </summary>
        [Deterministic]
        private static async Task LogWebhookAction(IDurableOrchestrationContext context, string partitionKey, string rowKey, string action)
        {
            await context.CallActivityAsync(
                CallNames.LogWebhook,
            new LogWebhookEntity(false,
                                  partitionKey,
                                  rowKey,
                                  context.CurrentUtcDateTime,
                                  action: action)
            );
        }
    }
}