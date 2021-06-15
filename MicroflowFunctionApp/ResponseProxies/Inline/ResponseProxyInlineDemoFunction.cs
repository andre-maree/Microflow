using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using Microflow.API;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

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
            HttpResponseMessage result = await MicroflowAPI.MicroflowHttpClient.PostAsJsonAsync(httpCall.Url, (ProcessId: httpCall.PartitionKey, StepId: httpCall.RowKey));
            
            if (result.IsSuccessStatusCode)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            }

            return new HttpResponseMessage(result.StatusCode);
        }
    }
}
