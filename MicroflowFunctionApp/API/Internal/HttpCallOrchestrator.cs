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
        [FunctionName("HttpCallOrchestrator")]
        public static async Task<MicroflowHttpResponse> HttpCallOrchestrator(
   [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var httpCall = context.GetInput<HttpCall>();

            DurableHttpRequest durableHttpRequest = MicroflowHelper.CreateMicroflowDurableHttpRequest(httpCall, context.InstanceId);

            DurableHttpResponse durableHttpResponse = await context.CallHttpAsync(durableHttpRequest);

            return durableHttpResponse.GetMicroflowResponse();
        }
    }
}
