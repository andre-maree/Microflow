using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.API.Internal
{
    public static class MicroflowInternalAPI
    {
        /// <summary>
        /// Inline http call, wait for response
        /// </summary>
        [FunctionName("HttpCallOrchestrator")]
        public static async Task<MicroflowHttpResponse> HttpCallOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            HttpCall httpCall = context.GetInput<HttpCall>();

            DurableHttpRequest durableHttpRequest = httpCall.CreateMicroflowDurableHttpRequest(context.InstanceId);

            DurableHttpResponse durableHttpResponse = await context.CallHttpAsync(durableHttpRequest);

            return durableHttpResponse.GetMicroflowResponse();
        }
    }
}
