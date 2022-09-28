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
    public class Program
    {
        public static readonly HttpClient HttpClient = new();
        private static string baseUrl = "http://localhost:7071/microflow/v1";
        private static string apibaseUrl = "http://localhost:5860/microflow/v1/";
        //private static string baseUrl = "https://microflowapp5675763456345345.azurewebsites.net";


        static async Task Main(string[] args)
        {
            var allConfigString = CompilerDirectiveMaker.MakeCompilerDirectiveString("UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT");

            var exclusionString = CompilerDirectiveMaker.GetCompilerDirectiveForOptionToExclude(true, "SCALEGROUPS", allConfigString);

            HttpClient.Timeout = TimeSpan.FromMinutes(30);

            await TestWorkflow();
        }

        private static (string Name, Microflow Microflow) Create()
        {
            var workflow = TestWorkflows.CreateTestWorkflow_SimpleSteps();
            //var workflow = Tests.CreateTestWorkflow_10StepsParallel();
            //var workflow = Tests.CreateTestWorkflow_Complex1();
            //var workflow = Tests.CreateTestWorkflow_110Steps();
            //var workflow2 = Tests.CreateTestWorkflow_110Steps();

            var microflow = new Microflow()
            {
                WorkflowName = "Myflow_ClientX2",
                WorkflowVersion = "v2.1",
                Steps = workflow,
                MergeFields = WorkflowManager.CreateMergeFields()//,
                //DefaultRetryOptions = new MicroflowRetryOptions()
            };

            string workflowName = string.IsNullOrWhiteSpace(microflow.WorkflowVersion)
                                ? microflow.WorkflowName
                                : $"{microflow.WorkflowName}@{microflow.WorkflowVersion}";

            return (workflowName, microflow);
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
                //string webhook = "myhook/myaction/mysub";
                //var setwebhooksteps = await HttpClient.GetAsync(apibaseUrl + "StepFlowControl/" + webhook);
                var createResult = Create();
                var microFlow = createResult.Microflow;

                //string webhook = "myhook/myaction/mysub";
                //// callback by step number
                microFlow.Step(1).WebhookId = "managerApproval";
                //microFlow.Step(1).WebhookSubStepsMapping = new();
                //microFlow.Step(1).WebhookSubStepsMapping.Add(new()
                //{
                //    WebhookAction = "decline",//microFlow.WorkflowName + "@" + microFlow.WorkflowVersion + "/managerApproval/decline",
                //    SubStepsToRunForAction = new List<int>() { 2 }
                //});
                //microFlow.Step(1).WebhookSubStepsMapping.Add(new()
                //{
                //    WebhookAction = "approve",
                //    SubStepsToRunForAction = new List<int>() { 3 }
                //});
                microFlow.Step(4).WaitForAllParents = false;

                //microFlow.Step(1).WebhookTimeoutSeconds = 3;
                //microFlow.Step(1).RetryOptions = new MicroflowRetryOptions() { BackoffCoefficient = 1, DelaySeconds = 1, MaxDelaySeconds = 1, MaxRetries = 2, TimeOutSeconds = 300 };
                //microFlow.Step(1).StopOnActionFailed = false;
                //microFlow.Step(1).SubStepsToRunForWebhookTimeout = new List<int>() { 3 };
                //microFlow.Step(1).WebhookAction = "myhook";
                //microFlow.Step(2).WebhookAction = "with/action"; 
                //microFlow.Step(2).WebhookAction = "act";
                //microFlow.Step(3).WebhookAction = "warra";
                //microFlow.Step(4).WaitForAllParents = false;
                //microFlow.Step(5).WebhookAction = "warra";
                //microFlow.Step(6).WebhookAction = "warra";
                //microFlow.Step(7).WebhookAction = "warra";
                //microFlow.Step(1).WebhookAction = "warra";
                //microFlow.Step(5).WebhookAction = "mycallbackXYZ";
                //microFlow.Step(5).CallbackTimeoutSeconds = 15;
                //microFlow.Step(5).StopOnActionFailed = false;
                //// scale groups:
                //string scalegroup = "myscalegroup";
                //foreach (Step step in microFlow.Steps)
                //{
                //    step.ScaleGroupId = scalegroup;
                //}

                //// set max count
                //var scaleres = await ScaleGroupsManager.SetMaxInstanceCountForScaleGroup("scalegroup", 5, baseUrl, HttpClient);
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
                //microFlow.Step(4).CalloutUrl = baseUrl + $"/MicroflowStart/{"MyProject_ClientX"}?globalkey={globalKey}";
                //microFlow.Step(4).AsynchronousPollingEnabled = false;

                // Upsert
                var result = await HttpClient.PostAsJsonAsync(baseUrl + "/UpsertWorkflow/", microFlow, new JsonSerializerOptions() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

                for (int i = 0; i < 1; i++)
                {
                    //await Task.Delay(200);

                    tasks.Add(HttpClient.GetAsync(baseUrl + $"/Start/{createResult.Name}?globalkey={globalKey}&loop={loop}"));
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

        //public static Dictionary<string, string> CreateMergeFields()
        //{
        //    string querystring = "?ProjectName=<ProjectName>&MainOrchestrationId=<MainOrchestrationId>&SubOrchestrationId=<SubOrchestrationId>&CallbackUrl=<CallbackUrl>&RunId=<RunId>&StepNumber=<StepNumber>&GlobalKey=<GlobalKey>";// &StepId=<StepId>";

        //    Dictionary<string, string> mergeFields = new();
        //    // use 
        //    mergeFields.Add("default_post_url", "https://reqbin.com/echo/post/json" + querystring);
        //    // set the callout url to the new SleepTestOrchestrator http normal function url
        //    //mergeFields.Add("default_post_url", baseUrl + "/SleepTestOrchestrator_HttpStart" + querystring);
        //    //mergeFields.Add("default_post_url", baseUrl + "/testpost" + querystring);

        //    return mergeFields;
        //}
    }
}
