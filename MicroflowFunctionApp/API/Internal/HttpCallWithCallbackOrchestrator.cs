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
        /// <returns>True or false to indicate success</returns>
        [FunctionName("HttpCallWithCallbackOrchestrator")]
        public static async Task<bool> HttpCallWithCallback(
   [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger inlog)
        {
            var log = context.CreateReplaySafeLogger(inlog);

            var httpCall = context.GetInput<HttpCall>();

            DurableHttpRequest durableHttpRequest = MicroflowHelper.CreateMicroflowDurableHttpRequest(httpCall, context.InstanceId);

            // http call outside of Microflow, this is the micro-service api call
            var result = await context.CallHttpAsync(durableHttpRequest);

            if (result.StatusCode != System.Net.HttpStatusCode.OK)
                return false;

            // TODO: always use https

            //var ts = TimeSpan.FromSeconds(20);
            //DateTime deadline = context.CurrentUtcDateTime.Add(ts);

            ////log.LogCritical("Sleeping for " + ts.Seconds + " seconds");

            //await context.CreateTimer(deadline, CancellationToken.None);

            log.LogCritical($"Waiting for callback: {Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}/api/{httpCall.CallBackAction}/{context.InstanceId}/{httpCall.RowKey}");

            // wait for the external event, set the timeout
            var actionResult = await context.WaitForExternalEvent<HttpResponseMessage>(httpCall.CallBackAction, TimeSpan.FromSeconds(httpCall.ActionTimeoutSeconds));

            // check for action failed
            if(!actionResult.IsSuccessStatusCode)
            {
                log.LogWarning($"Step {httpCall.RowKey} callback action {httpCall.CallBackAction} denied at {DateTime.Now.ToString("HH:mm:ss")}");

                // return false immediately to stop the workflow processing
                if (httpCall.StopOnActionFailed)
                    return false;

                return true;
            }
            else
            {
                log.LogWarning($"Step {httpCall.RowKey} callback action {httpCall.CallBackAction} successful at {DateTime.Now.ToString("HH:mm:ss")}");

                return true;
            }
        }
    }
}
