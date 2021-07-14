using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using static Microflow.Helpers.Constants;

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
        /// Get global state
        /// </summary>
        [FunctionName("getGlobalState")]
        public static async Task<HttpResponseMessage> GetGlobalState([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "GlobalState/{globalKey}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string globalKey)
        {
            EntityId globalStateId = new EntityId("GlobalState", globalKey);
            Task<EntityStateResponse<int>> stateTask = client.ReadEntityStateAsync<int>(globalStateId);

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);

            await stateTask;

            resp.Content = new StringContent(stateTask.Result.EntityState.ToString());

            return resp;
        }

        /// <summary>
        /// Get project state
        /// </summary>
        [FunctionName("getProjectState")]
        public static async Task<HttpResponseMessage> GetProjectState([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "ProjectState/{projectName}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string projectName)
        {
            EntityId runStateId = new EntityId("ProjectState", projectName);
            Task<EntityStateResponse<int>> stateTask = client.ReadEntityStateAsync<int>(runStateId);

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);

            await stateTask;

            resp.Content = new StringContent(stateTask.Result.EntityState.ToString());

            return resp;
        }
    }
}