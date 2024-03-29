﻿using System;
using System.Threading.Tasks;
using Microflow.Helpers;
using MicroflowModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
using static MicroflowModels.Constants;
#endif

namespace Microflow.HttpOrchestrators
{
    public static class MicroflowHttpCall
    {
        /// <summary>
        /// Inline http call, wait for response
        /// </summary>
        [Deterministic]
        [FunctionName(CallNames.HttpCallOrchestrator)]
        public static async Task<MicroflowHttpResponse> HttpCallOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            (HttpCall httpCall, MicroflowHttpResponse runObjectResponse) = context.GetInput<(HttpCall, MicroflowHttpResponse)>();

            #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
            //////////////////////////////////////
            bool doneAdd = false;
            bool doneSubtract = false;
            EntityId countId = new(MicroflowEntities.StepCount, httpCall.PartitionKey + httpCall.RowKey);
            ////////////////////////////////////////////////
#endif
            #endregion

            try
            {
                DurableHttpRequest durableHttpRequest = httpCall.CreateMicroflowDurableHttpRequest(context.InstanceId, runObjectResponse ?? new MicroflowHttpResponse());

                #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
                //////////////////////////////////////
                // set the per step in-progress count to count+1
                context.SignalEntity(countId, MicroflowEntityKeys.Add);
                doneAdd = true;
                ////////////////////////////////////////////////
#endif
                #endregion

                Task<DurableHttpResponse> durableHttpResponseTask = null;

                // log start
                if (!httpCall.IsHttpGet)
                {
                    Task logTask = LogMicroflowHttpData(context, durableHttpRequest.Content, httpCall.PartitionKey, httpCall.RowKey, httpCall.RunId, true);

                    durableHttpResponseTask = context.CallHttpAsync(durableHttpRequest);

                    await logTask;
                }
                else
                {
                    durableHttpResponseTask = context.CallHttpAsync(durableHttpRequest);
                }
                    
                await durableHttpResponseTask;

                #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
                //////////////////////////////////////
                // set the per step in-progress count to count-1
                context.SignalEntity(countId, MicroflowEntityKeys.Subtract);
                doneSubtract = true;

                ////////////////////////////////////////////////
#endif
                #endregion

                // log end
                await LogMicroflowHttpData(context, durableHttpResponseTask.Result.Content, httpCall.PartitionKey, httpCall.RowKey, httpCall.RunId, false);

                return durableHttpResponseTask.Result.GetMicroflowResponse(httpCall.ForwardResponseData);
            }
            catch (TimeoutException)
            {
                if (!httpCall.StopOnCalloutFailure)
                {
                    return new MicroflowHttpResponse()
                    {
                        CalloutOrWebhook = CalloutOrWebhook.Callout,
                        Success = false,
                        HttpResponseStatusCode = -408,
                        Content = $"Callout to {httpCall.CalloutUrl} timed out"
                    };
                }

                throw;
            }
            catch (Exception e)
            {
                if (!httpCall.StopOnCalloutFailure)
                {
                    return new MicroflowHttpResponse()
                    {
                        CalloutOrWebhook = CalloutOrWebhook.Callout,
                        Success = false,
                        HttpResponseStatusCode = -500,
                        Content = $"Callout to to {httpCall.CalloutUrl} failed - " + e.Message
                    };
                }

                throw;
            }

            #region Optional: no stepcount
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
            //////////////////////////////////////
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
        /// Log response
        /// </summary>
        private async static Task LogMicroflowHttpData(IDurableOrchestrationContext context, string  data, string workflowName, string stepNumber, string runId, bool isRequest)
        {
            await context.CallActivityAsync(
                CallNames.LogMicroflowHttpData,
                    ($"{workflowName}@{stepNumber}@{runId}@{context.InstanceId}", data, isRequest)
            );
        }
    }
}
