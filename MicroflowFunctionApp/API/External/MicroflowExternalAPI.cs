using System;
using System.Collections.Specialized;
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
        /// Called from Microflow.ExecuteStep to get the current step table config
        /// </summary>
        [FunctionName("GetStep")]
        public static async Task<IHttpCallWithRetries> GetStep([ActivityTrigger] ProjectRun projectRun) => await projectRun.GetStep();

        /// <summary>
        /// Called from Microflow.ExecuteStep to get the current state of the project
        /// </summary>
        //[FunctionName("GetState")]
        //public static async Task<int> GetState([ActivityTrigger] string projectId) => await MicroflowTableHelper.GetState(projectId);

        /// <summary>
        /// Called from Microflow.ExecuteStep to get the project
        /// </summary>
        //[FunctionName("GetProjectControl")]
        //public static async Task<ProjectControlEntity> GetProjectControl([ActivityTrigger] string projectId) => await MicroflowTableHelper.GetProjectControl(projectId);

        /// <summary>
        /// 
        /// </summary>
        //[FunctionName("Pause")]
        //public static async Task Pause([ActivityTrigger] ProjectControlEntity projectControlEntity) => await projectControlEntity.Pause();

        /// <summary>
        /// use this to test some things like causing an exception
        /// </summary>
        [FunctionName("testpost")]
        public static async Task<HttpResponseMessage> TestPost(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "testpost")] HttpRequestMessage req)
        {
            await Task.Delay(1000);

            if (req.Method == HttpMethod.Post)
            {
                string r = await req.Content.ReadAsStringAsync();

                MicroflowPostData result = JsonSerializer.Deserialize<MicroflowPostData>(r);

                if ((result.StepNumber == 9 || result.StepNumber == 8 || result.StepNumber == 10) && result.ProjectName.Equals("xxx"))
                {
                    //HttpResponseMessage result2 = await MicroflowHttpClient.HttpClient.GetAsync($"{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}/start/");
                    //var kgkg = 0;
                    await Task.Delay(9000);
                }
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
                    await Task.Delay(10000);
                
            }

            
            HttpResponseMessage resp = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            //    resp.Headers.Location = new Uri("http://localhost:7071/api/testpost");
            //resp.Content = new StringContent("wappa");
            return resp;
        }
    }
}
