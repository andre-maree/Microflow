using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MicroflowModels;
using MicroflowModels.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using static MicroflowModels.Constants;

namespace Microflow.API.External
{
    public static class MicroflowExternalApi
    {

        /// <summary>
        /// Called from Microflow.ExecuteStep to get the current step table config
        /// </summary>
        [FunctionName(CallNames.GetStep)]
        public static async Task<IHttpCallWithRetries> GetStep([ActivityTrigger] MicroflowRun workflowRun) => await workflowRun.GetStep();

        /// <summary>
        /// use this to test some things like causing an exception
        /// </summary>
        [FunctionName("testpost")]
        public static async Task<HttpResponseMessage> TestPost(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "testpost")] HttpRequestMessage req)
        {
            await Task.Delay(1000);

            //if (req.Method == HttpMethod.Post)
            //{
                string r = await req.Content.ReadAsStringAsync();

                MicroflowPostData result = JsonSerializer.Deserialize<MicroflowPostData>(r);

               if (result.StepNumber == 6 || result.StepNumber == 8 || result.StepNumber == 10)// && result.workflowName.Equals("xxx"))
                {
                //HttpResponseMessage result2 = await MicroflowHttpClient.HttpClient.GetAsync($"{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}/start/");
                //var kgkg = 0;
                await Task.Delay(5000);
            }
            //}
            //else
            //{
            //    NameValueCollection data = req.RequestUri.ParseQueryString();
            //    MicroflowPostData postData = new MicroflowPostData()
            //    {
            //        CallbackUrl = data["CallbackUrl"],
            //        MainOrchestrationId = data["MainOrchestrationId"],
            //        workflowName = data["workflowName"],
            //        RunId = data["RunId"],
            //        StepNumber = Convert.ToInt32(data["StepNumber"]),
            //        StepId = data["StepId"],
            //        SubOrchestrationId = data["SubOrchestrationId"],
            //        GlobalKey = data["GlobalKey"]
            //    };
            //        await Task.Delay(10000);

            //}


            HttpResponseMessage resp = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            //    resp.Headers.Location = new Uri("http://localhost:7071/api/testpost");
            //resp.Content = new StringContent("wappa");
            return resp;
        }
    }
}
