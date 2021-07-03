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
        public static readonly HttpClient HttpClient = new HttpClient();
        private static string baseUrl = "http://localhost:7071";
        //private static string baseUrl = "https://microflowapp20210703154326.azurewebsites.net";

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
                //var workflow = Tests.CreateTestWorkflow_110Steps();

                var project = new MicroflowProject()
                {
                    ProjectName = "MicroflowDemo",
                    Steps = workflow,
                    Loop = 1,
                    MergeFields = CreateMergeFields()
                };

                //List<int> topsteps = new List<int>();
                //foreach(var step in workflow)
                //{
                //    if(workflow.FindAll(c => c.SubSteps.Contains(step.StepId)).Count==0)
                //    {
                //        topsteps.Add(step.StepId);
                //    }
                //}
                var dr = JsonSerializer.Serialize(project);
                var tasks = new List<Task<HttpResponseMessage>>();

                // call Microflow insertorupdateproject when something ischanges in the workflow, but do not always call this when corcurrent multiple workflows
                var result = await HttpClient.PostAsJsonAsync(baseUrl + "/api/insertorupdateproject", project, new JsonSerializerOptions(JsonSerializerDefaults.General));
                var ri = await result.Content.ReadAsStringAsync();
                // singleton workflow instance
                //var result2 = await HttpClient.PostAsJsonAsync("http://localhost:7071/api/start/39806875-9c81-4736-81c0-9be562dae71e/", new ProjectBase() { ProjectName = "MicroflowDemo" }, new JsonSerializerOptions(JsonSerializerDefaults.General));
                
                // save project and get project
                //var saveprohectjson = await HttpClient.PostAsJsonAsync("http://localhost:7071/api/SaveProjectJson", project, new JsonSerializerOptions(JsonSerializerDefaults.General));
                //var getprohectjson = await HttpClient.PostAsJsonAsync("http://localhost:7071/api/GetProjectJsonAsLastSaved/" + project.ProjectName, new JsonSerializerOptions(JsonSerializerDefaults.General));
                //string content = await getprohectjson.Content.ReadAsStringAsync();
                //MicroflowProject microflowProject = JsonSerializer.Deserialize<MicroflowProject>(content);

                ////parallel multiple workflow instances
                for (int i = 0; i < 2; i++)
                {
                    await Task.Delay(100);
                    //await posttask;
                    tasks.Add(HttpClient.PostAsJsonAsync(baseUrl + "/api/start/", new MicroflowProjectBase() { ProjectName = "MicroflowDemo" }, new JsonSerializerOptions(JsonSerializerDefaults.General)));
                }

                ////await posttask;
                await Task.WhenAll(tasks);

                foreach(var t in tasks)
                {
                    if(t.IsFaulted || t.IsCanceled || !t.IsCompletedSuccessfully || !t.Result.IsSuccessStatusCode)
                    {
                        var r = await t.Result.Content.ReadAsStringAsync();
                    }
                }
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
            //mergeFields.Add("default_post_url", baseUrl + "/api/SleepTestOrchestrator_Function");// + querystring);
            mergeFields.Add("default_post_url", baseUrl + "/api/testpost");

            return mergeFields;
        }
    }
}
