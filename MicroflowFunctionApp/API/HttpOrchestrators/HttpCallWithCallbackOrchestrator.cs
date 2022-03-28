using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.Models;
using MicroflowModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using static MicroflowModels.Constants;

namespace Microflow.HttpOrchestrators
{
    public static class MicroflowHttpCallWithCallback
    {
        /// <summary>
        /// Does the call out and then waits for the webhook
        /// </summary>
        [Deterministic]
        [FunctionName("HttpCallWithCallbackOrchestrator")]
        public static async Task<MicroflowHttpResponse> HttpCallWithCallback([OrchestrationTrigger] IDurableOrchestrationContext context,
                                                                             ILogger inLog)
        {
            ILogger log = context.CreateReplaySafeLogger(inLog);

            HttpCall httpCall = context.GetInput<HttpCall>();

            DurableHttpRequest durableHttpRequest = httpCall.CreateMicroflowDurableHttpRequest(context.InstanceId);

            bool doneCallout = false;

            #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
            //////////////////////////////////////
            bool doneAdd = false;
            bool doneSubtract = false;
            EntityId countId = new(MicroflowEntities.StepCount, httpCall.PartitionKey + httpCall.RowKey);
            //////////////////////////////////////
#endif
            #endregion

            // http call outside of Microflow, this is the micro-service api call
            try
            {
                #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
                ////////////////////////////////////////////////
                // set the per step in-progress count to count+1
                context.SignalEntity(countId, MicroflowCounterKeys.Add);
                doneAdd = true;
                ////////////////////////////////////////////////
#endif
                #endregion

                DurableHttpResponse durableHttpResponse1 = await context.CallHttpAsync(durableHttpRequest);
                DurableHttpResponse durableHttpResponse = durableHttpResponse1;
                doneCallout = true;

                MicroflowHttpResponse microflowHttpResponse = durableHttpResponse.GetMicroflowResponse();

                // if failed http status return
                if (!microflowHttpResponse.Success)
                    return microflowHttpResponse;

                // TODO: always use https

                log.LogCritical($"Waiting for webhook: {CallNames.Webhook}/{httpCall.WebhookAction}/{context.InstanceId}/{httpCall.RowKey}");
                // wait for the external event, set the timeout
                HttpResponseMessage actionResult = await context.WaitForExternalEvent<HttpResponseMessage>(httpCall.WebhookAction,
                                                                                                           TimeSpan.FromSeconds(httpCall.WebhookTimeoutSeconds));
                #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
                ////////////////////////////////////////////////
                // set the per step in-progress count to count-1
                context.SignalEntity(countId, MicroflowCounterKeys.Subtract);
                doneSubtract = true;
                ////////////////////////////////////////////////
#endif
                #endregion

                // check for action failed
                if (actionResult.IsSuccessStatusCode)
                {
                    log.LogWarning($"Step {httpCall.RowKey} webhook {httpCall.WebhookAction} successful at {context.CurrentUtcDateTime:HH:mm:ss}");

                    microflowHttpResponse.HttpResponseStatusCode = (int)actionResult.StatusCode;

                    return microflowHttpResponse;
                }
                else
                {
                    if (!httpCall.StopOnActionFailed)
                    {
                        return new MicroflowHttpResponse()
                        {
                            Success = false,
                            HttpResponseStatusCode = (int)actionResult.StatusCode,
                            Message = $"webhook action {httpCall.WebhookAction} falied, StopOnActionFailed is {httpCall.StopOnActionFailed}"
                        };
                    }
                }
            }
            catch (TimeoutException)
            {
                if (!httpCall.StopOnActionFailed)
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = false,
                        HttpResponseStatusCode = -408,
                        Message = doneCallout
                        ? $"webhook action {httpCall.WebhookAction} timed out, StopOnActionFailed is {httpCall.StopOnActionFailed}"
                        : $"callout to {httpCall.CalloutUrl} timed out before spawning a webhook, StopOnActionFailed is {httpCall.StopOnActionFailed}"
                    };
                }

                throw;
            }
            catch (Exception e)
            {
                if (!httpCall.StopOnActionFailed)
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = false,
                        HttpResponseStatusCode = -500,
                        Message = doneCallout
                        ? $"webhook action {httpCall.WebhookAction} failed, StopOnActionFailed is {httpCall.StopOnActionFailed} - " + e.Message
                        : $"callout to {httpCall.CalloutUrl} failed before spawning a webhook, StopOnActionFailed is {httpCall.StopOnActionFailed}"
                    };
                }

                throw;
            }

            #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
            ////////////////////////////////////////////////finally
            {
                if (doneAdd && !doneSubtract)
                {
                    // set the per step in-progress count to count-1
                    context.SignalEntity(countId, MicroflowCounterKeys.Subtract);
                }
            }
            ////////////////////////////////////////////////
#endif
            #endregion

            return null;
        }
    }
}