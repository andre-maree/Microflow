using System.Net;
using System.Net.Http;
using System.Threading;
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

            DurableHttpRequest durableHttpRequest = MicroflowHelper.GetDurableHttpRequest(httpCall, context.InstanceId);

            var result = await context.CallHttpAsync(durableHttpRequest);

            //Task<HttpResponseMessage> task = context.CallActivityAsync<HttpResponseMessage>("httpcall2", httpCall);
            //HttpResponseMessage result = await task;

            return result.StatusCode == HttpStatusCode.OK;
        }


        /// <summary>
        /// This simulates an activity executing, replace with real call like an API call
        /// </summary>
        //[FunctionName("httpcall2")]
        //public static async Task<HttpResponseMessage> HttpCall2([ActivityTrigger] HttpCall httpCall, ILogger log)
        //{
        //    var cts = new CancellationTokenSource();
        //    cts.CancelAfter(30000);
        //    HttpResponseMessage result = await MicroflowHttpClient.HttpClient.PostAsJsonAsync(httpCall.Url, (ProcessId: httpCall.PartitionKey, StepId: httpCall.RowKey), cts.Token);

        //    if (result.IsSuccessStatusCode)
        //    {
        //        return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        //    }

        //    return new HttpResponseMessage(result.StatusCode);
        //}
    }
}
