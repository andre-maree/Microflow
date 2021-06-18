using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

            Random random = new Random();
            var ts = TimeSpan.FromSeconds(random.Next(10, 20));
            DateTime deadline = context.CurrentUtcDateTime.Add(ts);
            
            log.LogCritical("Sleeping for " + ts.Seconds + " seconds");

            await context.CreateTimer(deadline, CancellationToken.None);
        }

        /// <summary>
        /// Http entry point
        /// </summary>
        [FunctionName("SleepTestOrchestrator_HttpStart")]
        public static async Task<HttpResponseMessage> SleepTestOrchestrator_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await client.StartNewAsync("SleepTestOrchestrator", null);
            
            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}