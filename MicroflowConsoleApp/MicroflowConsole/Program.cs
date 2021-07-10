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
        //private static string baseUrl = "https://microflowappxxxxxxxxxxxxxxxxxxx.azurewebsites.net";

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
                var workflow2 = Tests.CreateTestWorkflow_Complex1();
                //var workflow = Tests.CreateTestWorkflow_110Steps();

                var project = new MicroflowProject()
                {
                    ProjectName = "xxx",
                    Steps = workflow,
                    Loop = 1,
                    MergeFields = CreateMergeFields()
                };

                var project2 = new MicroflowProject()
                {
                    ProjectName = "yyy",
                    Steps = workflow2,
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

                // call microflow from microflow
                project.Steps[3].CalloutUrl = baseUrl + $"/api/start/{project2.ProjectName}";
                //project.Steps[3].AsynchronousPollingEnabled = false;
                // call Microflow insertorupdateproject when something ischanges in the workflow, but do not always call this when corcurrent multiple workflows
                var result = await HttpClient.PostAsJsonAsync(baseUrl + "/api/insertorupdateproject", project, new JsonSerializerOptions(JsonSerializerDefaults.General)); 
                var result2 = await HttpClient.PostAsJsonAsync(baseUrl + "/api/insertorupdateproject", project2, new JsonSerializerOptions(JsonSerializerDefaults.General));
                //var content = await result.Content.ReadAsStringAsync();
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
                    await Task.Delay(500);
                    //await posttask;
                    tasks.Add(HttpClient.GetAsync(baseUrl + $"/api/start/{project.ProjectName}"));
                    //tasks.Add(HttpClient.GetAsync(baseUrl + $"/api/start/{project.ProjectName}/33306875-9c81-4736-81c0-9be562dae777"));
                }
                //await result;
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
                var rtrtr = 0;
            }
        }

        public static Dictionary<string, string> CreateMergeFields()
        {
            string querystring = "?ProjectName=<ProjectName>&MainOrchestrationId=<MainOrchestrationId>&SubOrchestrationId=<SubOrchestrationId>&CallbackUrl=<CallbackUrl>&RunId=<RunId>&StepNumber=<StepNumber>&GlobalKey=<GlobalKey>";// &StepId=<StepId>";

        Dictionary<string, string> mergeFields = new Dictionary<string, string>();
            // use 
            //mergeFields.Add("default_post_url", "https://reqbin.com/echo/post/json" + querystring);
            // set the callout url to the new SleepTestOrchestrator http normal function url
            //mergeFields.Add("default_post_url", baseUrl + "/api/SleepTestOrchestrator_Function");// + querystring);
            mergeFields.Add("default_post_url", baseUrl + "/api/testpost" + querystring);

            return mergeFields;
        }
    }
}
