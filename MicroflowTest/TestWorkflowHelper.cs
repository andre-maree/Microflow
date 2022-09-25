using MicroflowModels;
using MicroflowSDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace MicroflowTest
{
    public static class TestWorkflowHelper
    {
        public static Dictionary<string, string> Config = GetConfig();
        public static string BaseUrl = Config["BaseUrl"];
        public static readonly HttpClient HttpClient = new();

        static TestWorkflowHelper()
        {
        }

        public static Dictionary<string, string> GetConfig()
        {
            string path = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
            string pathf = Directory.GetParent(Directory.GetParent(Directory.GetParent(path).FullName).FullName).FullName + "\\config.json";

            StreamReader reader = new StreamReader(pathf);

            string jsonString = reader.ReadToEnd();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
        }

        public static List<Step> CreateTestWorkflow_SimpleSteps()
        {
            // create 4 steps from 1 to 4, each with a post url merge field
            List<Step> steps = WorkflowManager.CreateSteps(4, 1,"{default_post_url}");
            
            // some settings to explore
            //steps[0].IsHttpGet = true;
            //steps[0].CallbackTimeoutSeconds = 30;
            //steps[0].WebhookAction = "approve";
            //steps[0].CalloutUrl = "http://localhost:7071/SleepTestOrchestrator_HttpStart";
            //steps[0].SetRetryForStep(1, 2, 1);
            //steps[0].StopOnActionFailed = true;
            //steps[0].CalloutTimeoutSeconds = 190;

            // add child steps 2 and 3 to 1, steps 2 and 3 executes in parallel
            steps[1].AddSubSteps(steps[2], steps[3]);
            // add steps 2 and 3 as parents of step 4
            // step 4 will wait for both 2 and 3
            steps[4].AddParentSteps(steps[2], steps[3]);

            // remove the top placeholder
            steps.Remove(steps[0]);

            return steps;
        }

        public static List<Step> CreateTestWorkflow_10StepsParallel()
        {
            List<Step> steps = WorkflowManager.CreateSteps(14, 1, "{default_post_url}");

            steps[1].AddSubSteps(steps[2], steps[3]);
            steps[2].AddSubSteps(steps[4], steps[5], steps[6], steps[7], steps[8]);
            steps[3].AddSubSteps(steps[9], steps[10], steps[11], steps[12], steps[13]);
            // 2 groups of 5 parallel steps = 10 parallel steps
            steps[14].AddParentSteps(steps[4], steps[5], steps[6], steps[7], steps[8], steps[9], steps[10], steps[11], steps[12], steps[13]);

            steps.Remove(steps[0]);

            return steps;
        }

        public static List<Step> CreateTestWorkflow_Complex1()
        {
            List<Step> steps = WorkflowManager.CreateSteps(8, 1, "{default_post_url}");

            steps[1].AddSubSteps(steps[2], steps[3], steps[4]);
            steps[2].AddSubSteps(steps[5], steps[6]);
            steps[3].AddSubSteps(steps[6], steps[7]);
            steps[4].AddSubSteps(steps[6], steps[8]);
            steps[5].AddSubSteps(steps[3], steps[6]);
            steps[6].AddSubSteps(steps[8]);
            steps[7].AddSubSteps(steps[8]);

            steps.Remove(steps[0]);

            return steps;
        }


        public static List<Step> CreateTestWorkflow_110Steps()
        {
            List<Step> steps = WorkflowManager.CreateSteps(110, 1, "{default_post_url}");

            steps[1].AddSubStepRange(steps, 3, 11);
            steps[11].AddSubStepRange(steps, 13, 22);
            steps[22].AddSubStepRange(steps, 24, 33);
            steps[33].AddSubStepRange(steps, 35, 44);
            steps[44].AddSubStepRange(steps, 46, 55);
            steps[55].AddSubStepRange(steps, 57, 66);
            steps[66].AddSubStepRange(steps, 68, 77);
            steps[77].AddSubStepRange(steps, 79, 88);
            steps[88].AddSubStepRange(steps, 90, 99);
            steps[99].AddSubStepRange(steps, 101, 110);

            steps.Remove(steps[0]);

            return steps;
        }

        public static (MicroflowModels.Microflow microflow, string workflowName) CreateMicroflow(List<Step> workflow)
        {
            MicroflowModels.Microflow microflow = new()
            {
                WorkflowName = "Myflow_ClientX2",
                WorkflowVersion = "2.1",
                Steps = workflow,
                MergeFields = WorkflowManager.CreateMergeFields(),
                DefaultRetryOptions = new MicroflowRetryOptions()
            };

            string workflowName = string.IsNullOrWhiteSpace(microflow.WorkflowVersion)
                                ? microflow.WorkflowName
                                : $"{microflow.WorkflowName}@{microflow.WorkflowVersion}";

            return (microflow, workflowName);
        }

        public static async Task<(string, string)> StartMicroflow((MicroflowModels.Microflow workflow, string workflowName) microflow, List<Task<HttpResponseMessage>> tasks, int loop, string globalKey, bool waitForCompleted = true)
        {
            for (int i = 0; i < 1; i++)
            {
                //await Task.Delay(200);
                tasks.Add(TestWorkflowHelper.HttpClient.GetAsync(TestWorkflowHelper.BaseUrl + $"/Start/{microflow.workflowName}?globalkey={globalKey}&loop={loop}"));
                //tasks.Add(HttpClient.GetAsync(baseUrl + $"/MicroflowStart/{project.ProjectName}/33306875-9c81-4736-81c0-9be562dae777"));
            }

            HttpResponseMessage[] task = await Task.WhenAll(tasks);

            string instanceId = "";
            string statusUrl = "";

            if (task[0].StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                string content = await task[0].Content.ReadAsStringAsync();
                    OrchResult? res = JsonSerializer.Deserialize<OrchResult>(content);

                instanceId = res.id;
                statusUrl = res.statusQueryGetUri;

                while (true && waitForCompleted)
                {
                    await Task.Delay(2000);

                    HttpResponseMessage res2 = await HttpClient.GetAsync(res.statusQueryGetUri);
                    instanceId = res.id;

                    if (res2.StatusCode == System.Net.HttpStatusCode.OK)
                        break;
                }
            }

            return (instanceId, statusUrl);
        }

        public static async Task<bool> UpsertWorkFlow(MicroflowModels.Microflow workflow)
        {
            // Upsert
            var result = await TestWorkflowHelper.HttpClient.PostAsJsonAsync(TestWorkflowHelper.BaseUrl + "/UpsertWorkflow/", workflow, new JsonSerializerOptions(JsonSerializerDefaults.General));

            if(result.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return true;
            }

            return false;
        }
    }
}

namespace MicroflowTest
{
    public class OrchResult
    {
        public string id { get; set; }
        public string purgeHistoryDeleteUri { get; set; }
        public string sendEventPostUri { get; set; }
        public string statusQueryGetUri { get; set; }
        public string terminatePostUri { get; set; }
    }
}
