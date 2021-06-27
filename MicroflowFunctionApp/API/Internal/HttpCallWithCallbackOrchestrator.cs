using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microflow.API
{
    public static class HttpCallWithCallbackOrchestrator
    {
        /// <summary>
        /// Does the call out and then waits for the callback
        /// </summary>
        [FunctionName("HttpCallWithCallbackOrchestrator")]
        public static async Task<MicroflowHttpResponse> HttpCallWithCallback(
   [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger inlog)
        {
            var log = context.CreateReplaySafeLogger(inlog);

            var httpCall = context.GetInput<HttpCall>();

            DurableHttpRequest durableHttpRequest = MicroflowHelper.CreateMicroflowDurableHttpRequest(httpCall, context.InstanceId);

            // http call outside of Microflow, this is the micro-service api call
            DurableHttpResponse durableHttpResponse = await context.CallHttpAsync(durableHttpRequest);

            MicroflowHttpResponse microflowHttpResponse = durableHttpResponse.GetMicroflowResponse();

            // if failed http status return
            if (!microflowHttpResponse.Success)
                return microflowHttpResponse;

            // TODO: always use https

            log.LogCritical($"Waiting for callback: {Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}/api/{httpCall.CallBackAction}/{context.InstanceId}/{httpCall.RowKey}");

            // wait for the external event, set the timeout
            var actionResult = await context.WaitForExternalEvent<HttpResponseMessage>(httpCall.CallBackAction, TimeSpan.FromSeconds(httpCall.ActionTimeoutSeconds));

            // check for action failed
            if (actionResult.IsSuccessStatusCode)
            {
                log.LogWarning($"Step {httpCall.RowKey} callback action {httpCall.CallBackAction} successful at {DateTime.Now.ToString("HH:mm:ss")}");

                microflowHttpResponse.HttpResponseStatusCode = (int)actionResult.StatusCode;

                return microflowHttpResponse;
            }
            // if action callback failed
            else
            {
                log.LogWarning($"Step {httpCall.RowKey} callback action {httpCall.CallBackAction} fail result at {DateTime.Now.ToString("HH:mm:ss")}");

                microflowHttpResponse.Success = false;
                microflowHttpResponse.HttpResponseStatusCode = (int)actionResult.StatusCode;
                microflowHttpResponse.Message = $"callback action {httpCall.CallBackAction} fail - " + microflowHttpResponse.Message;

                return microflowHttpResponse;
            }
        }
    }
}