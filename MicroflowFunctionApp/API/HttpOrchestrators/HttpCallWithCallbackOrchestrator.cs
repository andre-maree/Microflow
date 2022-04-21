using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
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
        [FunctionName(CallNames.HttpCallWithWebhookOrchestrator)]
        public static async Task<MicroflowHttpResponse> HttpCallWithCallback([OrchestrationTrigger] IDurableOrchestrationContext context,
                                                                             ILogger inLog)
        {
            ILogger log = context.CreateReplaySafeLogger(inLog);

            (HttpCall httpCall, string content) input = context.GetInput<(HttpCall, string)>();

            Webhook webHook = JsonSerializer.Deserialize<Webhook>(input.httpCall.Webhook);

            //DurableHttpRequest durableHttpRequest = input.httpCall.CreateMicroflowDurableHttpRequest(context.InstanceId, input.content, webHook);

            //bool doneCallout = false;

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
                context.SignalEntity(countId, MicroflowEntityKeys.Add);
                doneAdd = true;
                ////////////////////////////////////////////////
#endif
                #endregion

                //DurableHttpResponse durableHttpResponse = await context.CallHttpAsync(durableHttpRequest);

                //doneCallout = true;

                //MicroflowHttpResponse microflowHttpResponse = durableHttpResponse.GetMicroflowResponse(false);

                // if failed http status return
                //if (!microflowHttpResponse.Success)
                //    return microflowHttpResponse;

                // TODO: always use https

                log.LogCritical($"Waiting for webhook: {CallNames.BaseUrl}/{webHook.UriPath}/{context.InstanceId}/{input.httpCall.RowKey}");
                // wait for the external event, set the timeout
                WebhookResult webhookResult = await context.WaitForExternalEvent<WebhookResult>(context.InstanceId,
                                                                                                           TimeSpan.FromSeconds(input.httpCall.WebhookTimeoutSeconds));

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
                if (webhookResult.StatusCode >= 200 && webhookResult.StatusCode < 300)
                {
                    MicroflowHttpResponse microflowHttpResponse = new() { Success = true };

                    log.LogWarning($"Step {input.httpCall.RowKey} webhook {webHook.UriPath} successful at {context.CurrentUtcDateTime:HH:mm:ss}");

                    microflowHttpResponse.HttpResponseStatusCode = webhookResult.StatusCode;

                    if (input.httpCall.ForwardPostData)
                    {
                        microflowHttpResponse.Message = webhookResult.Content;
                    }

                    microflowHttpResponse.SubStepsToRun = webhookResult.SubStepsToRun;

                    return microflowHttpResponse;
                }
                else
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = false,
                        HttpResponseStatusCode = webhookResult.StatusCode,
                        Message = $"webhook action {webHook.UriPath} falied, StopOnActionFailed is {input.httpCall.StopOnActionFailed}"
                    };
                }
            }
            catch (TimeoutException tex)
            {
                throw tex;
            }
            catch (Exception e)
            {
                if (!input.httpCall.StopOnActionFailed)
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = false,
                        HttpResponseStatusCode = -500,
                        Message = $"Webhook action failed, StopOnActionFailed is {input.httpCall.StopOnActionFailed} - " + e.Message
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
    }
}