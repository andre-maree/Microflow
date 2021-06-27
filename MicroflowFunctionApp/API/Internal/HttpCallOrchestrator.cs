using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.API
{
    public static class MicroflowInternalAPI
    {
        /// <summary>
        /// Inline http call, wait for response
        /// </summary>
        [FunctionName("HttpCallOrchestrator")]
        public static async Task<MicroflowHttpResponse> HttpCallOrchestrator(
   [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var httpCall = context.GetInput<HttpCall>();

            DurableHttpRequest durableHttpRequest = MicroflowHelper.CreateMicroflowDurableHttpRequest(httpCall, context.InstanceId);

            DurableHttpResponse durableHttpResponse = await context.CallHttpAsync(durableHttpRequest);

            return durableHttpResponse.GetMicroflowResponse();
        }
    }
}
