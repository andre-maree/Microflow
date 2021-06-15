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
        static async Task Main(string[] args)
        {
            using var client = new HttpClient();

            //var terminate = await client.PostAsync("http://localhost:7071/runtime/webhooks/durabletask/instances/39806875-9c81-4736-81c0-9be562dae71e/terminate?reason=dfgd", null);
            try
            {
                var workflow = Tests.CreateTestWorkflow_SimpleSteps();
                //var workflow = Tests.CreateTestWorkflow_Complex2();
                //var workflow = Tests.CreateTestWorkflow_10StepsParallel();
                //var workflow = Tests.CreateTestWorkflow_Complex1();

                var project = new Project() { 
                    ProjectName = "yellowtaildemo",
                    AllSteps = workflow, 
                    Loop = 1, 
                    MergeFields = CreateMergeFields() 
                };

                var tasks = new List<Task>();
                //var result = await client.PostAsJsonAsync("http://localhost:7071/api/start/39806875-9c81-4736-81c0-9be562dae71e/", project, new JsonSerializerOptions(JsonSerializerDefaults.General));
                //var result = await client.PostAsJsonAsync("https://microflowapp20210615143625.azurewebsites.net/api/start/39806875-9c81-4736-81c0-9be562dae71e/", project, new JsonSerializerOptions(JsonSerializerDefaults.General));
                for (int i = 0; i < 100; i++)
                {
                    await Task.Delay(500);
                    tasks.Add(client.PostAsJsonAsync("https://microflowapp20210615143625.azurewebsites.net/api/start/", project, new JsonSerializerOptions(JsonSerializerDefaults.General)));
                    //tasks.Add(client.PostAsJsonAsync("http://localhost:7071/api/start/", project, new JsonSerializerOptions(JsonSerializerDefaults.General)));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Console.ReadKey();
            }
        }

        public static Dictionary<string, string> CreateMergeFields()
        {
            Dictionary<string, string> mergeFields = new Dictionary<string, string>();
            // it may be that a default post url is not needed if all steps have a different post url
            mergeFields.Add("default_post_url", "https://reqbin.com/echo/post/json?workflowid=<workflowId>&processid=<stepId>");

            return mergeFields;
        }
    }
}
