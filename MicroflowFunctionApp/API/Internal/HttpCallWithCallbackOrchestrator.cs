using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microflow.API.Internal
{
    public static class HttpCallWithCallbackOrchestrator
    {
        /// <summary>
        /// Does the call out and then waits for the callback
        /// </summary>
        [FunctionName("HttpCallWithCallbackOrchestrator")]
        public static async Task<MicroflowHttpResponse> HttpCallWithCallback([OrchestrationTrigger] IDurableOrchestrationContext context,
                                                                             ILogger inLog)
        {
            ILogger log = context.CreateReplaySafeLogger(inLog);

            HttpCall httpCall = context.GetInput<HttpCall>();

            DurableHttpRequest durableHttpRequest = httpCall.CreateMicroflowDurableHttpRequest(context.InstanceId);

            // http call outside of Microflow, this is the micro-service api call
            
            try
            {
                DurableHttpResponse durableHttpResponse = await context.CallHttpAsync(durableHttpRequest);

                MicroflowHttpResponse microflowHttpResponse = durableHttpResponse.GetMicroflowResponse();

                // if failed http status return
                if (!microflowHttpResponse.Success)
                    return microflowHttpResponse;

                // TODO: always use https

                log.LogCritical($"Waiting for callback: {Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}/api/{httpCall.CallBackAction}/{context.InstanceId}/{httpCall.RowKey}");
                // wait for the external event, set the timeout
                HttpResponseMessage actionResult = await context.WaitForExternalEvent<HttpResponseMessage>(httpCall.CallBackAction, TimeSpan.FromSeconds(httpCall.ActionTimeoutSeconds));
                
                // check for action failed
                if (actionResult.IsSuccessStatusCode)
                {
                    log.LogWarning($"Step {httpCall.RowKey} callback action {httpCall.CallBackAction} successful at {DateTime.Now.ToString("HH:mm:ss")}");

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
                            Message = $"callback action {httpCall.CallBackAction} falied"
                        };
                    }
                }
            }
            catch(TimeoutException)
            {
                if(!httpCall.StopOnActionFailed)
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = false,
                        HttpResponseStatusCode = 408,
                        Message = $"callback action {httpCall.CallBackAction} timed out"
                    };
                }

                throw;
            }
            catch (Exception)
            {
                if (!httpCall.StopOnActionFailed)
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = false,
                        HttpResponseStatusCode = -999,
                        Message = $"callback action {httpCall.CallBackAction} failed"
                    };
                }

                throw;
            }

            throw new Exception("Unknown error for step " + httpCall.RowKey);
        }
    }
}