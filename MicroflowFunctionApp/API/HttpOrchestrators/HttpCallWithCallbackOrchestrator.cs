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
        [FunctionName(CallNames.HttpCallWithCallbackOrchestrator)]
        public static async Task<MicroflowHttpResponse> HttpCallWithCallback([OrchestrationTrigger] IDurableOrchestrationContext context,
                                                                             ILogger inLog)
        {
            ILogger log = context.CreateReplaySafeLogger(inLog);

            (HttpCall httpCall, string content) input = context.GetInput<(HttpCall, string)>();

            DurableHttpRequest durableHttpRequest = input.httpCall.CreateMicroflowDurableHttpRequest(context.InstanceId, input.Item2);

            bool doneCallout = false;

            #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
            //////////////////////////////////////
            bool doneAdd = false;
            bool doneSubtract = false;
            EntityId countId = new(MicroflowEntities.StepCount, input.httpCall.PartitionKey + input.httpCall.RowKey);
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

                log.LogCritical($"Waiting for webhook: {CallNames.Webhook}/{input.httpCall.WebhookAction}/{context.InstanceId}/{input.httpCall.RowKey}");
                // wait for the external event, set the timeout
                HttpResponseMessage actionResult = await context.WaitForExternalEvent<HttpResponseMessage>(input.httpCall.WebhookAction,
                                                                                                           TimeSpan.FromSeconds(input.httpCall.WebhookTimeoutSeconds));
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
                    log.LogWarning($"Step {input.httpCall.RowKey} webhook {input.httpCall.WebhookAction} successful at {context.CurrentUtcDateTime:HH:mm:ss}");

                    microflowHttpResponse.HttpResponseStatusCode = (int)actionResult.StatusCode;

                    return microflowHttpResponse;
                }
                else
                {
                    if (!input.httpCall.StopOnActionFailed)
                    {
                        return new MicroflowHttpResponse()
                        {
                            Success = false,
                            HttpResponseStatusCode = (int)actionResult.StatusCode,
                            Message = $"webhook action {input.httpCall.WebhookAction} falied, StopOnActionFailed is {input.httpCall.StopOnActionFailed}"
                        };
                    }
                }
            }
            catch (TimeoutException)
            {
                if (!input.httpCall.StopOnActionFailed)
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = false,
                        HttpResponseStatusCode = -408,
                        Message = doneCallout
                        ? $"webhook action {input.httpCall.WebhookAction} timed out, StopOnActionFailed is {input.httpCall.StopOnActionFailed}"
                        : $"callout to {input.httpCall.CalloutUrl} timed out before spawning a webhook, StopOnActionFailed is {input.httpCall.StopOnActionFailed}"
                    };
                }

                throw;
            }
            catch (Exception e)
            {
                if (!input.httpCall.StopOnActionFailed)
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = false,
                        HttpResponseStatusCode = -500,
                        Message = doneCallout
                        ? $"webhook action {input.httpCall.WebhookAction} failed, StopOnActionFailed is {input.httpCall.StopOnActionFailed} - " + e.Message
                        : $"callout to {input.httpCall.CalloutUrl} failed before spawning a webhook, StopOnActionFailed is {input.httpCall.StopOnActionFailed}"
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