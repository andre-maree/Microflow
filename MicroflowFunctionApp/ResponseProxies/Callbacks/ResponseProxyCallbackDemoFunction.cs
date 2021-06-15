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
        [FunctionName("callback")]
        public static async Task<HttpResponseMessage> RaiseEvent(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "{action}/{orchestratorId}/{stepId}/{cmd?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient client, string stepId, string action, string orchestratorId, string cmd)
        {
            //string data = await req.Content.ReadAsStringAsync();

            await client.RaiseEventAsync(orchestratorId, action, cmd);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }
}
