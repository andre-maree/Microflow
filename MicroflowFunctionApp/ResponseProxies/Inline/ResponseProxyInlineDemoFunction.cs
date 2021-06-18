using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microflow.API;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Threading;

namespace Microflow
{
    public static class ResponseProxyInlineDemoFunction
    {
        /// <summary>
        /// This simulates an activity executing, replace with real call like an API call
        /// </summary>
        [FunctionName("httpcall")]
        public static async Task<HttpResponseMessage> HttpCall([ActivityTrigger] HttpCall httpCall, ILogger log)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(60000);
            HttpResponseMessage result = await MicroflowHttpClient.HttpClient.PostAsJsonAsync(httpCall.Url, (ProcessId: httpCall.PartitionKey, StepId: httpCall.RowKey), cts.Token);
            
            if (result.IsSuccessStatusCode)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            }

            return new HttpResponseMessage(result.StatusCode);
        }
    }
}
