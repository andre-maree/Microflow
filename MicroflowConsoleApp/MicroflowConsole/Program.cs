using MicroflowModels;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace MicroflowConsole
{
    class Program
    {
        public static readonly HttpClient HttpClient = new HttpClient();
        private static string baseUrl = "http://localhost:7071";
        //private static string baseUrl = "https://microflowappXXXXXXXXXXXXX.azurewebsites.net";

        static async Task Main(string[] args)
        {
            await TestWorkflow();

            //var url = "";
            //var res = await HttpClient.p
        }

        private static async Task TestWorkflow()
        {
            HttpClient.Timeout = TimeSpan.FromMinutes(30);

            //var terminate = await client.PostAsync("http://localhost:7071/runtime/webhooks/durabletask/instances/39806875-9c81-4736-81c0-9be562dae71e/terminate?reason=dfgd", null);
            try
            {
                var workflow = Tests.CreateTestWorkflow_SimpleSteps();
                //var workflow = Tests.CreateTestWorkflow_10StepsParallel();
                //var workflow = Tests.CreateTestWorkflow_Complex1();

                var project = new Project()
                {
                    ProjectName = "MicroflowDemo",
                    Steps = workflow,
                    Loop = 1,
                    MergeFields = CreateMergeFields()
                };

                //var r = JsonSerializer.Serialize(project);
                var tasks = new List<Task>();

                
                var result = await HttpClient.PostAsJsonAsync("http://localhost:7071/api/insertorupdateproject", project, new JsonSerializerOptions(JsonSerializerDefaults.General));
                // singleton workflow instance
                //var result2 = await HttpClient.PostAsJsonAsync("http://localhost:7071/api/start/39806875-9c81-4736-81c0-9be562dae71e/", project, new JsonSerializerOptions(JsonSerializerDefaults.General));

                //HttpResponseMessage posttask = await client.PostAsJsonAsync(baseUrl + "/api/prepareproject/", project, new JsonSerializerOptions(JsonSerializerDefaults.General));
                ////parallel multiple workflow instances
                for (int i = 0; i < 1; i++)
                {
                    await Task.Delay(500);
                    //await posttask;
                    tasks.Add(HttpClient.PostAsJsonAsync(baseUrl + "/api/start/", project, new JsonSerializerOptions(JsonSerializerDefaults.General)));
                }

                ////await posttask;
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Console.ReadKey();
            }
        }

        public static Dictionary<string, string> CreateMergeFields()
        {
            string querystring = "?ProjectName=<ProjectName>&MainOrchestrationId=<MainOrchestrationId>&SubOrchestrationId=<SubOrchestrationId>&CallbackUrl=<CallbackUrl>&RunId=<RunId>&StepId=<StepId>";

        Dictionary<string, string> mergeFields = new Dictionary<string, string>();
            // use 
            //mergeFields.Add("default_post_url", "https://reqbin.com/echo/post/json" + querystring);
            // set the callout url to the new SleepTestOrchestrator http normal function url
            mergeFields.Add("default_post_url", baseUrl + "/api/SleepTestOrchestrator_Function");// + querystring);

            return mergeFields;
        }
    }
}
