using System;
using System.Collections.Specialized;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MicroflowModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Microflow.API.Internal
{
    /// <summary>
    /// Use this to mock an orchestration micro-service for testing purposes,
    /// this will be moved to its own function app in the future,
    /// because it`s better to split the action/micro-service workers from the Microflow app for scalability
    /// </summary>
    public static class SleepTestOrchestrator
    {
        /// <summary>
        /// Sleep for between random min and max seconds
        /// </summary>
        [FunctionName("SleepTestOrchestrator")]
        public static async Task SleepTestMethod(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger inlog)
        {
            var log = context.CreateReplaySafeLogger(inlog);

            string data = context.GetInput<string>();
            MicroflowPostData postData = JsonSerializer.Deserialize<MicroflowPostData>(data);

            Random random = new Random();
            var ts = TimeSpan.FromSeconds(random.Next(3, 10));
            DateTime deadline = context.CurrentUtcDateTime.Add(ts);

            //log.LogCritical("Sleeping for " + ts.Seconds + " seconds");

            await context.CreateTimer(deadline, CancellationToken.None);

            //do the call back if there is 1
            if (!string.IsNullOrWhiteSpace(postData.CallbackUrl))
                {
                    DurableHttpRequest req = new DurableHttpRequest(HttpMethod.Get, new Uri("http://" + postData.CallbackUrl));

                    DurableHttpResponse resp = null;
                    try
                    {
                        log.LogCritical($"Callback Now!!!!");
                        resp = await context.CallHttpAsync(req);
                    }
                    catch (Exception ex)
                    {
                        var r = 0;
                    }
                }
        }

        /// <summary>
        /// Http entry point
        /// </summary>
        [FunctionName("SleepTestOrchestrator_HttpStart")]
        public static async Task<HttpResponseMessage> SleepTestOrchestrator_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient client)
        {
            if (req.Method == HttpMethod.Post)
            {
                string data = await req.Content.ReadAsStringAsync();

                string instanceId = await client.StartNewAsync("SleepTestOrchestrator", null, data);

                return client.CreateCheckStatusResponse(req, instanceId);
            }
            else
            {
                NameValueCollection data = req.RequestUri.ParseQueryString();
                MicroflowPostData postData = new MicroflowPostData()
                {
                    CallbackUrl = data["CallbackUrl"],
                    MainOrchestrationId = data["MainOrchestrationId"],
                    ProjectName = data["ProjectName"],
                    RunId = data["RunId"],
                    StepId = data["StepId"],
                    SubOrchestrationId = data["SubOrchestrationId"]
                };
                //MicroflowPostData body = JsonSerializer.Deserialize<MicroflowPostData>(data);
                // Function input comes from the request content.
                string instanceId = await client.StartNewAsync("SleepTestOrchestrator", null, JsonSerializer.Serialize(postData));

                return client.CreateCheckStatusResponse(req, instanceId);
            }
        }
    }
}