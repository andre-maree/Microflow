using System.Net.Http;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace MicroflowApiFunctionApp
{
    public static class RunTimeApi
    {

        /// <summary>
        /// Call this to see the step count in StepCallout per project name and stepId 
        /// </summary>
        [FunctionName("GetStepCountInprogress")]
        public static async Task<int> GetStepCountInprogress([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "getstepcountinprogress/{projectNameStepNumber}")] HttpRequestMessage req,
                                                             [DurableClient] IDurableEntityClient client,
                                                             string projectNameStepNumber)
        {
            EntityId countId = new EntityId(MicroflowEntities.StepCounter, projectNameStepNumber);

            EntityStateResponse<int> result = await client.ReadEntityStateAsync<int>(countId);

            return result.EntityState;
        }

        /// <summary>
        /// 
        /// </summary>
        //[FunctionName("Pause")]
        //public static async Task Pause([ActivityTrigger] ProjectControlEntity projectControlEntity) => await projectControlEntity.Pause();

        //[FunctionName("Function1")]
        //public static async Task<List<string>> RunOrchestrator(
        //    [OrchestrationTrigger] IDurableOrchestrationContext context)
        //{
        //    var outputs = new List<string>();

        //    // Replace "hello" with the name of your Durable Activity Function.
        //    outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "Tokyo"));
        //    outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "Seattle"));
        //    outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "London"));

        //    // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
        //    return outputs;
        //}

        //[FunctionName("Function1_Hello")]
        //public static string SayHello([ActivityTrigger] string name, ILogger log)
        //{
        //    log.LogInformation($"Saying hello to {name}.");
        //    return $"Hello {name}!";
        //}

        //[FunctionName("Function1_HttpStart")]
        //public static async Task<HttpResponseMessage> HttpStart(
        //    [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
        //    [DurableClient] IDurableOrchestrationClient starter,
        //    ILogger log)
        //{
        //    // Function input comes from the request content.
        //    string instanceId = await starter.StartNewAsync("Function1", null);

        //    log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

        //    return starter.CreateCheckStatusResponse(req, instanceId);
        //}
    }
}