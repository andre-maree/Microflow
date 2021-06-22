using MicroflowModels;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace MicroflowConsole
{
    class Program
    {
        //private static string baseUrl = "http://localhost:7071";
        private static string baseUrl = "https://microflowapp20210622163955.azurewebsites.net";

        static async Task Main(string[] args)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(30);

            //var terminate = await client.PostAsync("http://localhost:7071/runtime/webhooks/durabletask/instances/39806875-9c81-4736-81c0-9be562dae71e/terminate?reason=dfgd", null);
            try
            {
                //var workflow = Tests.CreateTestWorkflow_SimpleSteps();
                //var workflow = Tests.CreateTestWorkflow_10StepsParallel();
                var workflow = Tests.CreateTestWorkflow_Complex1();

                var project = new Project() { 
                    ProjectName = "MicroflowDemo",
                    Steps = workflow, 
                    Loop = 1, 
                    MergeFields = CreateMergeFields() 
                };

                //var r = JsonSerializer.Serialize(project);
                var tasks = new List<Task>();

                // singleton workflow instance
                //var result = await client.PostAsJsonAsync("http://localhost:7071/api/start/39806875-9c81-4736-81c0-9be562dae71e/", project, new JsonSerializerOptions(JsonSerializerDefaults.General));

                //Task<HttpResponseMessage> posttask = client.PostAsJsonAsync(baseUrl + "/api/start/", project, new JsonSerializerOptions(JsonSerializerDefaults.General)); 
                //parallel multiple workflow instances
                for (int i = 0; i < 200; i++)
                {
                    await Task.Delay(500);
                    //await posttask;
                    tasks.Add(client.PostAsJsonAsync(baseUrl + "/api/start/", project, new JsonSerializerOptions(JsonSerializerDefaults.General)));
                }

                //await posttask;
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

            mergeFields.Add("default_post_url", baseUrl + "/api/SleepTestOrchestrator_HttpStart" + querystring);

            return mergeFields;
        }
    }
}
