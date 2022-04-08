using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microflow.Models;

namespace Microflow.Webhook
{
    public static class Webhook
    {
        /// <summary>
        /// These client functions can be refactored into separate function apps, 
        /// this being custom code that might change more frequently than the workflow engine core,
        /// and will then also scale on its own.
        /// </summary>
        [FunctionName("Webhook")]
        public static async Task<HttpResponseMessage> RaiseEvent(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "/" + MicroflowModels.Constants.MicroflowPath + "/Webhook/{action}/{orchestratorId}/{stepId}/{fail:bool?}")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient client, int stepId, string action, string orchestratorId, bool? fail)
        {
            string content = await req.Content.ReadAsStringAsync();

            await client.RaiseEventAsync(orchestratorId, action, new WebhookResult() 
            { 
                StatusCode = !fail.HasValue || fail.Value == true ? 200 : -418,  
                Content = content 
            });

            return new(HttpStatusCode.OK);
        }
    }
}
