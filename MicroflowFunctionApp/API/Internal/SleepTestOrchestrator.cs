using System;
using System.Collections.Specialized;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MicroflowModels;
using Microsoft.AspNetCore.Http;
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
        [Deterministic]
        [FunctionName("SleepTestOrchestrator")]
        public static async Task SleepTestMethod(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger inLog)
        {
            ILogger log = context.CreateReplaySafeLogger(inLog);

            string data = context.GetInput<string>();
            MicroflowPostData postData = JsonSerializer.Deserialize<MicroflowPostData>(data);

            Random random = new Random();
            TimeSpan ts = TimeSpan.FromSeconds(random.Next(30, 40));
            DateTime deadline = context.CurrentUtcDateTime.Add(ts);

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                try
                {
                    //cts.CancelAfter(60000);
                    await context.CreateTimer(deadline, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    log.LogCritical("========================TaskCanceledException==========================");
                }
                finally
                {
                    cts.Dispose();
                }
            }
            // test if the callback is done, do the call back if there is 1
            if (!string.IsNullOrWhiteSpace(postData.CallbackUrl))
            {
                DurableHttpRequest req = new DurableHttpRequest(HttpMethod.Get, new Uri(postData.CallbackUrl));

                await context.CallHttpAsync(req);
            }
        }

        /// <summary>
        /// Called by the normal function and creates a new SleepTestOrchestrator
        /// </summary>
        [FunctionName("SleepTestOrchestrator_HttpStart")]
        public static async Task<HttpResponseMessage> SleepTestOrchestrator_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "SleepTestOrchestrator_HttpStart")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient client)
        {
            string instanceId = Guid.NewGuid().ToString();
            if (req.Method == HttpMethod.Post)
            {
                string data = await req.Content.ReadAsStringAsync();

                await client.StartNewAsync("SleepTestOrchestrator", instanceId, data);

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
                    StepNumber = Convert.ToInt32(data["StepNumber"]),
                    StepId = data["StepId"],
                    SubOrchestrationId = data["SubOrchestrationId"],
                    GlobalKey = data["GlobalKey"]
                };

                // Function input comes from the request content.
                await client.StartNewAsync("SleepTestOrchestrator", instanceId, JsonSerializer.Serialize(postData));
                
                return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// Http entry point as a normal function calling a client function
        /// </summary>
        [FunctionName("SleepTestOrchestrator_Function")]
        public static async Task<HttpResponseMessage> Run(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            MicroflowPostData data = JsonSerializer.Deserialize<MicroflowPostData>(requestBody);

            await MicroflowHttpClient.HttpClient.PostAsJsonAsync("http://localhost:7071/api/SleepTestOrchestrator_HttpStart/", data);

            HttpResponseMessage resp = new HttpResponseMessage();

            // test the returned status codes here and also the effect if Microflows step setting StopOnActionFailed
            //resp.StatusCode = System.Net.HttpStatusCode.NotFound;
            resp.StatusCode = System.Net.HttpStatusCode.OK;
            // set the location and check in the stpe log if its saved when 201 created
            //resp.Headers.Location = new Uri("http://localhost:7071/api/SleepTestOrchestrator_HttpStart/");

            return resp;
        }
    }
}