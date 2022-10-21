using MicroflowModels;
using MicroflowSDK;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
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
            List<Step> steps = WorkflowManager.CreateSteps(4, 1, "{default_post_url}");

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

        public static (MicroflowModels.Microflow microflow, string workflowName) CreateMicroflow(List<Step> workflow, bool? passThroughParams = true, bool? isHttpGet = false)
        {
            PassThroughParams paramss = new();

            if (passThroughParams.Value == false)
            {
                paramss.GlobalKey = false;
                paramss.MainOrchestrationId = false;
                paramss.RunId = false;
                paramss.StepId = false;
                paramss.StepNumber = false;
                paramss.SubOrchestrationId = false;
                paramss.WebhookId = false;
                paramss.WorkflowName = false;
            }

            MicroflowModels.Microflow microflow = new()
            {
                WorkflowName = "Unit_test_workflow",
                WorkflowVersion = "1.0",
                Steps = workflow,
                MergeFields = CreateMergeFields(paramss, isHttpGet.Value),
                DefaultRetryOptions = new RetryOptions()
            };

            string workflowName = string.IsNullOrWhiteSpace(microflow.WorkflowVersion)
                                ? microflow.WorkflowName
                                : $"{microflow.WorkflowName}@{microflow.WorkflowVersion}";

            return (microflow, workflowName);
        }

        public static async Task<HttpResponseMessage> StartMicroflow((MicroflowModels.Microflow workflow, string workflowName) microflow, int loop, string globalKey, string? postData = null)
        {
            if (postData == null)
            {
                return await HttpClient.GetAsync(BaseUrl + $"/Start/{microflow.workflowName}?globalkey={globalKey}&loop={loop}");
            }

            return await HttpClient.PostAsync(BaseUrl + $"/Start/{microflow.workflowName}?globalkey={globalKey}&loop={loop}", new StringContent(postData));
        }

        public static async Task SetScaleGroupMax(int maxConcurrentInstanceCount, string scaleGroupId)
        {
            // set scale group
            HttpResponseMessage scaleGroupSet = await ScaleGroupsManager.SetMaxInstanceCountForScaleGroup(scaleGroupId, maxConcurrentInstanceCount, BaseUrl, HttpClient);

            Assert.IsTrue(scaleGroupSet.StatusCode == System.Net.HttpStatusCode.OK);

            Dictionary<string, int> scaleGroupGet = await ScaleGroupsManager.GetScaleGroupsWithMaxInstanceCounts(scaleGroupId, BaseUrl, HttpClient);
            Assert.IsTrue(scaleGroupGet[scaleGroupId] == maxConcurrentInstanceCount);
        }

        public static Dictionary<string, string> CreateMergeFields(PassThroughParams passThroughParams, bool IsGet)
        {
            string method = IsGet ? "get" : "post";
            PropertyInfo[] props = passThroughParams.GetType().GetProperties();
            string querystring = "?";
            foreach (var param in props)
            {
                var val = param.GetValue(passThroughParams);
                if ((bool)val == true)
                {
                    querystring += $"{param.Name}=<{param.Name}>&";
                }
            }
            querystring = querystring.Remove(querystring.Length - 1);
            //string querystring2 = "?WorkflowName=<WorkflowName>&MainOrchestrationId=<MainOrchestrationId>&SubOrchestrationId=<SubOrchestrationId>&Webhook=<Webhook>&RunId=<RunId>&StepNumber=<StepNumber>&GlobalKey=<GlobalKey>&StepId=<StepId>";

            Dictionary<string, string> mergeFields = new();
            // use 
            mergeFields.Add("default_post_url", $"https://reqbin.com/echo/{method}/json" + querystring);
            // set the callout url to the new SleepTestOrchestrator http normal function url
            //mergeFields.Add("default_post_url", baseUrl + "/SleepTestOrchestrator_HttpStart" + querystring);
            //mergeFields.Add("default_post_url", baseUrl + "/testpost" + querystring);

            return mergeFields;
        }
    }
}