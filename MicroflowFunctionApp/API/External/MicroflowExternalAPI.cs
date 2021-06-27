using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace Microflow.API.External
{
    public static class MicroflowExternalAPI
    {
        /// <summary>
        /// Called from Microflow.ExecuteStep to get the current step table config
        /// </summary>
        [FunctionName("GetStep")]
        public static async Task<HttpCallWithRetries> GetStep([ActivityTrigger] ProjectRun projectRun) => await MicroflowTableHelper.GetStep(projectRun);

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
        public static async Task Pause([ActivityTrigger] ProjectControlEntity projectControlEntity) => await MicroflowTableHelper.Pause(projectControlEntity);

        /// <summary>
        /// use this to test some things like causing an exception
        /// </summary>
        [FunctionName("testpost")]
        public static async Task<HttpResponseMessage> TestPost(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "testpost")] HttpRequestMessage req)
        {
            await Task.Delay(1500);
            await req.Content.ReadAsStringAsync();

            //MicroflowPostData result = JsonSerializer.Deserialize<MicroflowPostData>(r);

            //if (result.StepId.Equals("1"))
            //{
            //    return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            //}
            var resp = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            //    resp.Headers.Location = new Uri("http://localhost:7071/api/testpost");
            //resp.Content = new StringContent("wappa");
            return resp;
        }
    }
}
