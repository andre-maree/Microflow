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

            (MicroflowPostData postData, string action) = context.GetInput<(MicroflowPostData, string)>();

            Random random = new();
            TimeSpan ts = TimeSpan.FromSeconds(random.Next(1, 5));
            DateTime deadline = context.CurrentUtcDateTime.Add(ts);

            using (CancellationTokenSource cts = new())
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
            // test if the webhook is done, do the call back if there is 1
            if (!string.IsNullOrWhiteSpace(postData.Webhook))
            {
                postData.Webhook += string.IsNullOrEmpty(action) ? "" : "/" + action;
            
                DurableHttpRequest req = new(HttpMethod.Get, new Uri(postData.Webhook));

                await context.CallHttpAsync(req);
            }

            return;
        }

        /// <summary>
        /// Called by the normal function and creates a new SleepTestOrchestrator
        /// </summary>
        [FunctionName("SleepTestOrchestrator_HttpStart")]
        public static async Task<HttpResponseMessage> SleepTestOrchestrator_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "SleepTestOrchestrator_HttpStart/{webhookAction?}/{isAsync:bool?}")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient client, string webhookAction, bool? isAsync)
        {
            if (!isAsync.HasValue)
            {
                isAsync = false;
            }

            MicroflowPostData postData;

            string instanceId = Guid.NewGuid().ToString();

            if (req.Method == HttpMethod.Post)
            {
                string data = await req.Content.ReadAsStringAsync();

                postData = JsonSerializer.Deserialize<MicroflowPostData>(data);

                await client.StartNewAsync("SleepTestOrchestrator", instanceId, (postData, webhookAction));
            }
            else
            {
                NameValueCollection data = req.RequestUri.ParseQueryString();

                postData = new()
                {
                    Webhook = data["Webhook"],
                    MainOrchestrationId = data["MainOrchestrationId"],
                    WorkflowName = data["WorkflowName"],
                    RunId = data["RunId"],
                    StepNumber = Convert.ToInt32(data["StepNumber"]),
                    StepId = data["StepId"],
                    SubOrchestrationId = data["SubOrchestrationId"],
                    GlobalKey = data["GlobalKey"]
                };
                
                await client.StartNewAsync("SleepTestOrchestrator", instanceId, (postData, webhookAction));
            }

            if(isAsync.Value)
            {
                return client.CreateCheckStatusResponse(req, instanceId);
            }
            else if (!string.IsNullOrEmpty(postData.Webhook))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"success\":\"true\"}\n")
                };
            }
            else
            {
                await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromSeconds(1000));

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"success\":\"true\"}\n")
                };
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

            await MicroflowHttpClient.HttpClient.PostAsJsonAsync($"{Environment.GetEnvironmentVariable("BaseUrl")}SleepTestOrchestrator_HttpStart/", data);

            HttpResponseMessage resp = new();

            // test the returned status codes here and also the effect if Microflows step setting StopOnWebhookTimeout
            //resp.StatusCode = System.Net.HttpStatusCode.NotFound;
            resp.StatusCode = System.Net.HttpStatusCode.OK;
            // set the location and check in the stpe log if its saved when 201 created
            //resp.Headers.Location = new Uri("http://localhost:7071/api/SleepTestOrchestrator_HttpStart/");

            return resp;
        }
    }
}