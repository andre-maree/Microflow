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
        [FunctionName("callback")]
        public static async Task<HttpResponseMessage> RaiseEvent(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "webhook/{action}/{orchestratorId}/{stepId}/{content?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient client, string stepId, string action, string orchestratorId, string content)
        {
            //string data = await req.Content.ReadAsStringAsync();

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);

            // pass content back
            if (!string.IsNullOrWhiteSpace(content))
            {
                resp.Content = new StringContent(content);
            }
            
            await client.RaiseEventAsync(orchestratorId, action, resp);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
