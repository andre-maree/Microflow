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
        //private static string baseUrl = "https://microflowapp5675763456345345.azurewebsites.net";

        static async Task Main(string[] args)
        {
            await TestWorkflow();

            //var url = "";
            //var res = await HttpClient.p
        }

        private static async Task TestWorkflow()
        {
            //// Create the array.
            //Array myArray = Array.CreateInstance(typeof(double), new int[1] { 12 }, new int[1] { 1 });

            //// Fill the array with random values.
            //Random rand = new Random();
            //for (int index = myArray.GetLowerBound(0); index <= myArray.GetUpperBound(0); index++)
            //{
            //    myArray.SetValue(rand.NextDouble(), index);
            //}

            //// Display the values.
            //for (int index = myArray.GetLowerBound(0); index <= myArray.GetUpperBound(0); index++)
            //{
            //    Console.WriteLine("myArray[{0}] = {1}", index, myArray.GetValue(index));
            //}


            HttpClient.Timeout = TimeSpan.FromMinutes(30);

            //var terminate = await client.PostAsync("http://localhost:7071/runtime/webhooks/durabletask/instances/39806875-9c81-4736-81c0-9be562dae71e/terminate?reason=dfgd", null);
            try
            {
                //var workflow = Tests.CreateTestWorkflow_SimpleSteps();
                var workflow = Tests.CreateTestWorkflow_10StepsParallel();
                //var workflow = Tests.CreateTestWorkflow_Complex1();
                //var workflow = Tests.CreateTestWorkflow_110Steps();
                //var workflow2 = Tests.CreateTestWorkflow_110Steps();

                var project = new Microflow()
                {
                    WorkflowName = "MyProject_ClientX",
                    Steps = workflow,
                    Loop = 1,
                    MergeFields = CreateMergeFields(), 
                    DefaultRetryOptions = new MicroflowRetryOptions()
                };

                string scalegroup = "myscalegroup4";
                //workflow[0].ScaleGroupId = "myscalegroup";
                //workflow[0].CallbackAction = "approve";
                workflow[3].ScaleGroupId = scalegroup;
                workflow[4].ScaleGroupId = scalegroup;
                workflow[5].ScaleGroupId = scalegroup;
                workflow[6].ScaleGroupId = scalegroup;
                workflow[7].ScaleGroupId = scalegroup;
                workflow[8].ScaleGroupId = scalegroup;
                workflow[9].ScaleGroupId = scalegroup;
                workflow[10].ScaleGroupId = scalegroup;
                workflow[11].ScaleGroupId = scalegroup;
                workflow[12].ScaleGroupId = scalegroup;
                //var project2 = new MicroflowProject()
                //{
                //    ProjectName = "yyy",
                //    Steps = workflow2,
                //    Loop = 1,
                //    MergeFields = CreateMergeFields()
                //};

                //List<int> topsteps = new List<int>();
                //foreach(var step in workflow)
                //{
                //    if(workflow.FindAll(c => c.SubSteps.Contains(step.StepId)).Count==0)
                //    {
                //        topsteps.Add(step.StepId);
                //    }
                //}
                //var dr = JsonSerializer.Serialize(project);
                var tasks = new List<Task<HttpResponseMessage>>();

                // call microflow from microflow
                //project.Steps[2].CalloutUrl = baseUrl + $"/start/{project2.ProjectName}";
                //project.Steps[0].AsynchronousPollingEnabled = false;
                // call Microflow insertorupdateproject when something ischanges in the workflow, but do not always call this when corcurrent multiple workflows
                var result = await HttpClient.PostAsJsonAsync(baseUrl + "/UpsertWorkflow/", project, new JsonSerializerOptions(JsonSerializerDefaults.General));

                //// scale groups:
                //// set max count
                var scaleres = await SetMaxInstanceCountForScaleGroup(scalegroup, 5);
                //// get max count for group
                //var getScaleGroup = await GetScaleGroupsWithMaxInstanceCounts(scalegroup);
                //// get max counts for all groups
                //var getScaleGroup2 = await GetScaleGroupsWithMaxInstanceCounts(null);

                //project.Steps[0].CalloutUrl = project.Steps[5].CalloutUrl;
                //var result2 = HttpClient.PostAsJsonAsync(baseUrl + "/insertorupdateproject/", project2, new JsonSerializerOptions(JsonSerializerDefaults.General));
                //var content = await result.Content.ReadAsStringAsync();
                // singleton workflow instance
                //var result2 = await HttpClient.PostAsJsonAsync("http://localhost:7071/api/start/39806875-9c81-4736-81c0-9be562dae71e/", new ProjectBase() { ProjectName = "MicroflowDemo" }, new JsonSerializerOptions(JsonSerializerDefaults.General));

                // save project and get project
                //var saveprohectjson = await HttpClient.PostAsJsonAsync("http://localhost:7071/api/SaveProjectJson", project, new JsonSerializerOptions(JsonSerializerDefaults.General));
                //var getprohectjson = await HttpClient.PostAsJsonAsync("http://localhost:7071/api/GetProjectJson/" + project.ProjectName, new JsonSerializerOptions(JsonSerializerDefaults.General));

                //string content = await getprohectjson.Content.ReadAsStringAsync();
                //MicroflowProject microflowProject = JsonSerializer.Deserialize<MicroflowProject>(content);
                ////parallel multiple workflow instances

                //await result;
                //await result2;

                for (int i = 0; i < 1; i++)
                {
                    await Task.Delay(500);
                    //await posttask;
                    tasks.Add(HttpClient.GetAsync(baseUrl + $"/start/{project.WorkflowName}?globalkey=globber"));
                    //tasks.Add(HttpClient.GetAsync(baseUrl + $"/start/{project.ProjectName}/33306875-9c81-4736-81c0-9be562dae777"));
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
                li = JsonSerializer.Deserialize<Dictionary<string,int>>(t);
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
            mergeFields.Add("default_post_url", "https://reqbin.com/echo/post/json");// + querystring);
            // set the callout url to the new SleepTestOrchestrator http normal function url
            //mergeFields.Add("default_post_url", baseUrl + "/SleepTestOrchestrator_HttpStart" + querystring);
            //mergeFields.Add("default_post_url", baseUrl + "/testpost" + querystring);

            return mergeFields;
        }
    }
}
