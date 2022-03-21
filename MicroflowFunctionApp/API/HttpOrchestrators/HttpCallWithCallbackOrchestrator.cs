﻿using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.Models;
using MicroflowModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using static MicroflowModels.Constants.Constants;

namespace Microflow.HttpOrchestrators
{
    public static class MicroflowHttpCallWithCallback
    {
        /// <summary>
        /// Does the call out and then waits for the callback
        /// </summary>
        [Deterministic]
        [FunctionName("HttpCallWithCallbackOrchestrator")]
        public static async Task<MicroflowHttpResponse> HttpCallWithCallback([OrchestrationTrigger] IDurableOrchestrationContext context,
                                                                             ILogger inLog)
        {
            ILogger log = context.CreateReplaySafeLogger(inLog);

            HttpCall httpCall = context.GetInput<HttpCall>();

            DurableHttpRequest durableHttpRequest = httpCall.CreateMicroflowDurableHttpRequest(context.InstanceId);

            bool doneAdd = false;
            bool doneSubtract = false;
            bool doneCallout = false;
            EntityId countId = new EntityId(MicroflowEntities.StepCount, httpCall.PartitionKey + httpCall.RowKey);

            // http call outside of Microflow, this is the micro-service api call
            try
            {
                // set the per step in-progress count to count+1
                context.SignalEntity(countId, MicroflowCounterKeys.Add);
                doneAdd = true;

                DurableHttpResponse durableHttpResponse = await context.CallHttpAsync(durableHttpRequest);
                doneCallout = true;

                MicroflowHttpResponse microflowHttpResponse = durableHttpResponse.GetMicroflowResponse();

                // if failed http status return
                if (!microflowHttpResponse.Success)
                    return microflowHttpResponse;

                // TODO: always use https

                log.LogCritical($"Waiting for callback: {Environment.GetEnvironmentVariable("BaseUrl")}{CallNames.CallbackBase}/{httpCall.CallbackAction}/{context.InstanceId}/{httpCall.RowKey}");
                // wait for the external event, set the timeout
                HttpResponseMessage actionResult = await context.WaitForExternalEvent<HttpResponseMessage>(httpCall.CallbackAction,
                                                                                                           TimeSpan.FromSeconds(httpCall.CallbackTimeoutSeconds));

                // set the per step in-progress count to count-1
                context.SignalEntity(countId, MicroflowCounterKeys.Subtract);
                doneSubtract = true;

                // check for action failed
                if (actionResult.IsSuccessStatusCode)
                {
                    log.LogWarning($"Step {httpCall.RowKey} callback action {httpCall.CallbackAction} successful at {context.CurrentUtcDateTime:HH:mm:ss}");

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
                            Message = $"callback action {httpCall.CallbackAction} falied, StopOnActionFailed is {httpCall.StopOnActionFailed}"
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
                        ? $"callback action {httpCall.CallbackAction} timed out, StopOnActionFailed is {httpCall.StopOnActionFailed}"
                        : $"callout to {httpCall.CalloutUrl} timed out before spawning a callback, StopOnActionFailed is {httpCall.StopOnActionFailed}"
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
                        ? $"callback action {httpCall.CallbackAction} failed, StopOnActionFailed is {httpCall.StopOnActionFailed} - " + e.Message
                        : $"callout to {httpCall.CalloutUrl} failed before spawning a callback, StopOnActionFailed is {httpCall.StopOnActionFailed}"
                    };
                }

                throw;
            }
            finally
            {
                if (doneAdd && !doneSubtract)
                {
                    // set the per step in-progress count to count-1
                    context.SignalEntity(countId, MicroflowCounterKeys.Subtract);
                }
            }

            return null;
        }
    }
}