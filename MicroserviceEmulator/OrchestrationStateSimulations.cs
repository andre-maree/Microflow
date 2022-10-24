using System;
using System.Collections.Generic;
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
    public static class OrchestrationStateSimulations
    {
        [FunctionName("RunningOrchestration")]
        public static async Task<List<string>> RunningOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            List<string> outputs = new();

            outputs.Add(await context.CallActivityAsync<string>("Arb_Activity", "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>("Arb_Activity", "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>("Arb_Activity", "London"));

            //int o = 0;
            //int t = 5 / o;
            
            return outputs;
        }

        [FunctionName("Arb_Activity")]
        public static async Task<string> Arb_Activity([ActivityTrigger] string name, ILogger log)
        {
            await Task.Delay(30000);

            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName("EmulateRunningOrchestration")]
        public static async Task<HttpResponseMessage> EmulateRunningOrchestration(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "EmulateRunningOrchestration")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync("RunningOrchestration", instanceId: "timer_qwerty");

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("EmulateCompletedOrchestration")]
        public static async Task<HttpResponseMessage> EmulateCompletedOrchestration(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "EmulateCompletedOrchestration")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync("RunningOrchestration", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, timeout:TimeSpan.FromMinutes(100));
        }
    }
}