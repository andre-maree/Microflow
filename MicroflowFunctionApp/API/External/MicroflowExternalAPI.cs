using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.Models;
using MicroflowModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace Microflow.API.External
{
    public static class MicroflowExternalApi
    {
        /// <summary>
        /// Call this to get the Json data of the project
        /// </summary>
        [FunctionName("GetProjectJson")]
        public static async Task<string> GetProjectJson([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "getprojectjson/{projectName}")] HttpRequestMessage req,
                                                        string projectName)
        {
            return MicroflowTableHelper.GetProjectAsJson(projectName); 
        }

        /// <summary>
        /// Call this to see the step count in StepCallout per project name and stepId 
        /// </summary>
        [FunctionName("GetStepCountInprogress")]
        public static async Task<int> GetStepCountInprogress([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "getstepcountinprogress/{projectNameStepId}")] HttpRequestMessage req,
                                                             [DurableClient] IDurableEntityClient client,
                                                             string projectNameStepId)
        {
            EntityId countId = new EntityId("StepCounter", projectNameStepId);

            EntityStateResponse<int> result = await client.ReadEntityStateAsync<int>(countId);

            return result.EntityState;
        }

        /// <summary>
        /// Called from Microflow.ExecuteStep to get the current step table config
        /// </summary>
        [FunctionName("GetStep")]
        public static async Task<HttpCallWithRetries> GetStep([ActivityTrigger] ProjectRun projectRun) => await projectRun.GetStep();

        /// <summary>
        /// Called from Microflow.ExecuteStep to get the current state of the project
        /// </summary>
        [FunctionName("GetState")]
        public static async Task<int> GetState([ActivityTrigger] string projectId) => await MicroflowTableHelper.GetState(projectId);

        /// <summary>
        /// Called from Microflow.ExecuteStep to get the project
        /// </summary>
        [FunctionName("GetProjectControl")]
        public static async Task<ProjectControlEntity> GetProjectControl([ActivityTrigger] string projectId) => await MicroflowTableHelper.GetProjectControl(projectId);

        /// <summary>
        /// 
        /// </summary>
        [FunctionName("Pause")]
        public static async Task Pause([ActivityTrigger] ProjectControlEntity projectControlEntity) => await projectControlEntity.Pause();

        /// <summary>
        /// use this to test some things like causing an exception
        /// </summary>
        [FunctionName("testpost")]
        public static async Task<HttpResponseMessage> TestPost(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "testpost")] HttpRequestMessage req)
        {
            await Task.Delay(1000);
            string r = await req.Content.ReadAsStringAsync();

            MicroflowPostData result = JsonSerializer.Deserialize<MicroflowPostData>(r);

            if (result.StepId.Equals("2"))
            {
                await Task.Delay(10000);
            }
            HttpResponseMessage resp = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            //    resp.Headers.Location = new Uri("http://localhost:7071/api/testpost");
            //resp.Content = new StringContent("wappa");
            return resp;
        }
    }
}
