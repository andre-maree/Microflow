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
        public static async Task<HttpCallWithRetries> GetStep([ActivityTrigger] ProjectRun projectRun)
        {
            try
            {
                return await MicroflowTableHelper.GetStep(projectRun);
            }
            catch (StorageException)
            {
                throw;
            }
        }

        /// <summary>
        /// Called from Microflow.ExecuteStep to get the current state of the project
        /// </summary>
        [FunctionName("GetState")]
        public static async Task<int> GetState([ActivityTrigger] string projectId)
        {
            try
            {
                return await MicroflowTableHelper.GetState(projectId);
            }
            catch (StorageException ex)
            {
                throw;
            }
        }

        /// <summary>
        /// Called from Microflow.ExecuteStep to get the project
        /// </summary>
        [FunctionName("GetProjectControl")]
        public static async Task<ProjectControlEntity> GetProjectControl([ActivityTrigger] string projectId)
        {
            try
            {
                return await MicroflowTableHelper.GetProjectControl(projectId);
            }
            catch (StorageException ex)
            {
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        [FunctionName("Pause")]
        public static async Task Pause([ActivityTrigger] ProjectControlEntity projectControlEntity)
        {
            try
            {
                await MicroflowTableHelper.Pause(projectControlEntity);
            }
            catch (StorageException ex)
            {
                throw;
            }
        }

        /// <summary>
        /// use this to test some things like causing an exception
        /// </summary>
        [FunctionName("testpost")]
        public static async Task<HttpResponseMessage> TestPost(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "testpost")] HttpRequestMessage req)
        {
            await Task.Delay(15000);
            await req.Content.ReadAsStringAsync();

            //MicroflowPostData result = JsonSerializer.Deserialize<MicroflowPostData>(r);

            //if (result.StepId.Equals("1"))
            //{
            //    return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            //}

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }
}
