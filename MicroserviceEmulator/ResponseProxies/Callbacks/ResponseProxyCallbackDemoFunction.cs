using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.ResponseProxies
{
    public static class ResponseProxyCallbackDemoFunction
    {
        /// <summary>
        /// These client functions can be refactored into separate function apps, 
        /// this being custom code that might change more frequently than the workflow engine core,
        /// and will then also scale on its own.
        /// </summary>
        [FunctionName("webhook")]
        public static async Task<HttpResponseMessage> RaiseEvent(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "webhook/{action}/{orchestratorId}/{stepId:int?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient client, int stepId, string action, string orchestratorId)
        {
            HttpResponseMessage resp = new(HttpStatusCode.OK);
            
            await client.RaiseEventAsync(orchestratorId, action, resp);

            return resp;
        }
    }
}
