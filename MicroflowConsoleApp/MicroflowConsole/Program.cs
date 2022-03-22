using MicroflowModels;
using MicroflowSDK;
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
        //private static string baseUrl = "https://microflowapp5675763456345345.azurewebsites.net";

        static async Task Main(string[] args)
        {
            HttpClient.Timeout = TimeSpan.FromMinutes(30);

            await TestWorkflow();
        }

        /// <summary>
        /// Play area for Microflow, take it for a spin
        /// </summary>
        private static async Task TestWorkflow()
        {
            var tasks = new List<Task<HttpResponseMessage>>();

            int loop = 1;
            string globalKey = "myGlobalKey";

            //var terminate = await client.PostAsync("http://localhost:7071/runtime/webhooks/durabletask/instances/39806875-9c81-4736-81c0-9be562dae71e/terminate?reason=dfgd", null);
            try
            {
                var workflow = Tests.CreateTestWorkflow_SimpleSteps();
                //var workflow = Tests.CreateTestWorkflow_10StepsParallel();
                //var workflow = Tests.CreateTestWorkflow_Complex1();
                //var workflow = Tests.CreateTestWorkflow_110Steps();
                //var workflow2 = Tests.CreateTestWorkflow_110Steps();

                Microflow microFlow = new Microflow()
                {
                    WorkflowName = "MyProject_Client2",
                    Steps = workflow,
                    MergeFields = CreateMergeFields(),
                    DefaultRetryOptions = new MicroflowRetryOptions()
                };

                //// callback by step number
                //microFlow.Step(2).CallbackAction = "warra";
                //microFlow.Step(5).CallbackAction = "mycallbackXYZ";
                //microFlow.Step(5).CallbackTimeoutSeconds = 15;
                //microFlow.Step(5).StopOnActionFailed = false;
                //// scale groups:
                //string scalegroup = "myscalegroup";
                //foreach (Step step in microFlow.Steps)
                //{
                //    step.ScaleGroupId = scalegroup;
                //}

                //// set max count
                //var scaleres = await SetMaxInstanceCountForScaleGroup(scalegroup, 5);
                //// get max count for group
                //var getScaleGroup = await GetScaleGroupsWithMaxInstanceCounts(scalegroup);
                //// get max counts for all groups
                //var getScaleGroup2 = await GetScaleGroupsWithMaxInstanceCounts(null);

                //// set retry policy for step
                //StepsManager.SetRetryForSteps(5, 5, 3, 120, 5, new Step[] { microFlow.Step(5) });

                //// call microflow from microflow
                //var project2 = new MicroflowProject()
                //{
                //    ProjectName = "yyy",
                //    Steps = workflow2,
                //    Loop = 1,
                //    MergeFields = CreateMergeFields()
                //};
                
                //// call other workflow
                //microFlow.Step(4).CalloutUrl = baseUrl + $"/MicroflowStart/{"MyProject_Client1"}?globalkey={globalKey}";
                //microFlow.Step(4).AsynchronousPollingEnabled = false;

                //var result = await HttpClient.PostAsJsonAsync(baseUrl + "/UpsertWorkflow/", microFlow, new JsonSerializerOptions(JsonSerializerDefaults.General));

                for (int i = 0; i < 1; i++)
                {
                    await Task.Delay(200);

                    tasks.Add(HttpClient.GetAsync(baseUrl + $"/MicroflowStart/{microFlow.WorkflowName}?globalkey={globalKey}&loop={loop}"));
                    //tasks.Add(HttpClient.GetAsync(baseUrl + $"/MicroflowStart/{project.ProjectName}/33306875-9c81-4736-81c0-9be562dae777"));
                }

                await Task.WhenAll(tasks);

                foreach (var t in tasks)
                {
                    if (t.IsFaulted || t.IsCanceled || !t.IsCompletedSuccessfully || !t.Result.IsSuccessStatusCode)
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

        private static async Task<HttpResponseMessage> SetMaxInstanceCountForScaleGroup(string scaleGroupId, int maxInstanceCount)
        {
            return await HttpClient.PostAsJsonAsync($"http://localhost:7071/ScaleGroup/{scaleGroupId}/{maxInstanceCount}", new JsonSerializerOptions(JsonSerializerDefaults.General));
        }

        private static async Task<Dictionary<string, int>> GetScaleGroupsWithMaxInstanceCounts(string scaleGroupId)
        {
            Dictionary<string, int> li = null;

            if (!string.IsNullOrWhiteSpace(scaleGroupId))
            {
                string t = await HttpClient.GetStringAsync($"http://localhost:7071/api/ScaleGroup/{scaleGroupId}");
                li = JsonSerializer.Deserialize<Dictionary<string, int>>(t);
            }
            else
            {
                string t = await HttpClient.GetStringAsync($"http://localhost:7071/api/ScaleGroup");
                li = JsonSerializer.Deserialize<Dictionary<string, int>>(t);
            }

            return li;
        }

        public static Dictionary<string, string> CreateMergeFields()
        {
            string querystring = "?ProjectName=<ProjectName>&MainOrchestrationId=<MainOrchestrationId>&SubOrchestrationId=<SubOrchestrationId>&CallbackUrl=<CallbackUrl>&RunId=<RunId>&StepNumber=<StepNumber>&GlobalKey=<GlobalKey>";// &StepId=<StepId>";

            Dictionary<string, string> mergeFields = new Dictionary<string, string>();
            // use 
            mergeFields.Add("default_post_url", "https://reqbin.com/echo/post/json" + querystring);
            // set the callout url to the new SleepTestOrchestrator http normal function url
            //mergeFields.Add("default_post_url", baseUrl + "/SleepTestOrchestrator_HttpStart" + querystring);
            //mergeFields.Add("default_post_url", baseUrl + "/testpost" + querystring);

            return mergeFields;
        }
    }
}
