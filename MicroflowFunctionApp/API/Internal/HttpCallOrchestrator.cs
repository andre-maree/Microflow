using System.Net;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microflow.API
{
    public static class MicroflowInternalAPI
    {
        /// <summary>
        /// Inline http call, wait for response
        /// </summary>
        /// <returns>True or false to indicate success</returns>
        [FunctionName("HttpCallOrchestrator")]
        public static async Task<bool> HttpCallOrchestrator(
   [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var httpCall = context.GetInput<HttpCall>();

            DurableHttpRequest durableHttpRequest = MicroflowHelper.CreateMicroflowDurableHttpRequest(httpCall, context.InstanceId);

            var result = await context.CallHttpAsync(durableHttpRequest);

            //Task<HttpResponseMessage> task = context.CallActivityAsync<HttpResponseMessage>("httpcall2", httpCall);
            //HttpResponseMessage result = await task;

            return result.StatusCode == HttpStatusCode.OK;
        }
    }
}
