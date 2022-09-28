using MicroflowModels;
using MicroflowSDK;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

            // add child steps 2 and 3 to 1, steps 2 and 3 executes in parallel
            steps.StepNumber(1).AddSubSteps(steps.StepNumber(2), steps.StepNumber(3));

            // add steps 2 and 3 as parents of step 4
            // step 4 will wait for both 2 and 3
            steps.StepNumber(4).AddParentSteps(steps.StepNumber(2), steps.StepNumber(3));

            return steps;
        }

        public static List<Step> CreateTestWorkflow_10StepsParallel()
        {
            List<Step> steps = WorkflowManager.CreateSteps(14, 1, "{default_post_url}");

            steps.StepNumber(1).AddSubSteps(steps.StepNumber(2), steps.StepNumber(3));
            steps.StepNumber(2).AddSubSteps(steps.StepNumber(4), steps.StepNumber(5), steps.StepNumber(6), steps.StepNumber(7), steps.StepNumber(8));
            steps.StepNumber(3).AddSubSteps(steps.StepNumber(9), steps.StepNumber(10), steps.StepNumber(11), steps.StepNumber(12), steps.StepNumber(13));
            // 2 groups of 5 parallel steps = 10 parallel steps
            steps.StepNumber(14).AddParentSteps(steps.StepNumber(4), steps.StepNumber(5), steps.StepNumber(6), steps.StepNumber(7), steps.StepNumber(8), steps.StepNumber(9), steps.StepNumber(10), steps.StepNumber(11), steps.StepNumber(12), steps.StepNumber(13));

            return steps;
        }

        public static List<Step> CreateTestWorkflow_Complex1()
        {
            List<Step> steps = WorkflowManager.CreateSteps(8, 1, "{default_post_url}");

            steps.StepNumber(1).AddSubSteps(steps.StepNumber(2), steps.StepNumber(3), steps.StepNumber(4));
            steps.StepNumber(2).AddSubSteps(steps.StepNumber(5), steps.StepNumber(6));
            steps.StepNumber(3).AddSubSteps(steps.StepNumber(6), steps.StepNumber(7));
            steps.StepNumber(4).AddSubSteps(steps.StepNumber(6), steps.StepNumber(8));
            steps.StepNumber(5).AddSubSteps(steps.StepNumber(3), steps.StepNumber(6));
            steps.StepNumber(6).AddSubSteps(steps.StepNumber(8));
            steps.StepNumber(7).AddSubSteps(steps.StepNumber(8));

            return steps;
        }


        public static List<Step> CreateTestWorkflow_110Steps()
        {
            List<Step> steps = WorkflowManager.CreateSteps(110, 1, "{default_post_url}");

            steps.StepNumber(1).AddSubStepRange(steps, 3, 11);
            steps.StepNumber(11).AddSubStepRange(steps, 13, 22);
            steps.StepNumber(22).AddSubStepRange(steps, 24, 33);
            steps.StepNumber(33).AddSubStepRange(steps, 35, 44);
            steps.StepNumber(44).AddSubStepRange(steps, 46, 55);
            steps.StepNumber(55).AddSubStepRange(steps, 57, 66);
            steps.StepNumber(66).AddSubStepRange(steps, 68, 77);
            steps.StepNumber(77).AddSubStepRange(steps, 79, 88);
            steps.StepNumber(88).AddSubStepRange(steps, 90, 99);
            steps.StepNumber(99).AddSubStepRange(steps, 101, 110);

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

        public static async Task<(string, string)> StartMicroflow((MicroflowModels.Microflow workflow, string workflowName) microflow, int loop, string globalKey, bool waitForCompleted = true)
        {
            HttpResponseMessage task = await TestWorkflowHelper.HttpClient.GetAsync(TestWorkflowHelper.BaseUrl + $"/Start/{microflow.workflowName}?globalkey={globalKey}&loop={loop}");

            string instanceId = "";
            string statusUrl = "";

            if (task.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                string content = await task.Content.ReadAsStringAsync();
                    OrchResult? res = JsonSerializer.Deserialize<OrchResult>(content);

                instanceId = res.id;
                statusUrl = res.statusQueryGetUri;

                while (true && waitForCompleted)
                {
                    await Task.Delay(2000);

                    HttpResponseMessage res2 = await HttpClient.GetAsync(res.statusQueryGetUri);
                    string res2result = await res2.Content.ReadAsStringAsync();

                    if (res2result.Contains("\"runtimeStatus\":\"Completed\""))
                        break;
                }
            }

            return (instanceId, statusUrl);
        }

        public static async Task<bool> UpsertWorkFlow(MicroflowModels.Microflow workflow)
        {
            // Upsert
            HttpResponseMessage result = await TestWorkflowHelper.HttpClient.PostAsJsonAsync(TestWorkflowHelper.BaseUrl + "/UpsertWorkflow/", workflow, new JsonSerializerOptions(JsonSerializerDefaults.General));

            if(result.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return true;
            }

            return false;
        }

        public static async Task SetScaleGroupMax(int maxConcurrentInstanceCount, string scaleGroupId)
        {
            // set scale group
            HttpResponseMessage scaleGroupSet = await ScaleGroupsManager.SetMaxInstanceCountForScaleGroup(scaleGroupId, maxConcurrentInstanceCount, BaseUrl, HttpClient);

            Assert.IsTrue(scaleGroupSet.StatusCode == System.Net.HttpStatusCode.OK);

            Dictionary<string, int> scaleGroupGet = await ScaleGroupsManager.GetScaleGroupsWithMaxInstanceCounts(scaleGroupId, BaseUrl, HttpClient);
            Assert.IsTrue(scaleGroupGet[scaleGroupId] == maxConcurrentInstanceCount);
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
