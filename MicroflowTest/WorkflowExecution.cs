using MicroflowModels;
using MicroflowSDK;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace MicroflowTest
{
    [TestClass]
    public class WorkflowExecution
    {
        public static readonly HttpClient HttpClient = new HttpClient();
        private static string baseUrl = "http://localhost:7071/microflow/v1";

        [TestMethod]
        public async Task CreateTestWorkflow_Complex1()
        {
            string path = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory);
            string pathf = Directory.GetParent(Directory.GetParent(Directory.GetParent(path).FullName).FullName).FullName + "\\config.json";

            StreamReader r = new StreamReader(pathf);


            string jsonString = r.ReadToEnd();

            var ff = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);

            var workflow = TestWorkflows.CreateTestWorkflow_Complex1();
            //var workflow = TestWorkflows.CreateTestWorkflow_SimpleSteps();

            var microflow = new MicroflowModels.Microflow()
            {
                WorkflowName = "Myflow_ClientX2",
                WorkflowVersion = "v2.1",
                Steps = workflow,
                MergeFields = TestWorkflows.CreateMergeFields(),
                DefaultRetryOptions = new MicroflowRetryOptions()
            };

            string workflowName = string.IsNullOrWhiteSpace(microflow.WorkflowVersion)
                                ? microflow.WorkflowName
                                : $"{microflow.WorkflowName}@{microflow.WorkflowVersion}";

            var tasks = new List<Task<HttpResponseMessage>>();

            int loop = 1;
            string globalKey = Guid.NewGuid().ToString();

            //microflow.Step(1).WaitForAllParents = false;

            //microflow.Step(2).WaitForAllParents = false;
            //microflow.Step(3).WaitForAllParents = false;
            //microflow.Step(4).WaitForAllParents = false;
            //microflow.Step(5).WaitForAllParents = false;
            //microflow.Step(6).WaitForAllParents = false;
            //microflow.Step(7).WaitForAllParents = false;
            //microflow.Step(8).WaitForAllParents = false;

            // Upsert
            var result = await HttpClient.PostAsJsonAsync(baseUrl + "/UpsertWorkflow/", microflow, new JsonSerializerOptions(JsonSerializerDefaults.General));

            //Assert.IsTrue(result.StatusCode == System.Net.HttpStatusCode.OK);

            for (int i = 0; i < 1; i++)
            {
                //await Task.Delay(200);
                tasks.Add(HttpClient.GetAsync(baseUrl + $"/Start/{workflowName}?globalkey={globalKey}&loop={loop}"));
                //tasks.Add(HttpClient.GetAsync(baseUrl + $"/MicroflowStart/{project.ProjectName}/33306875-9c81-4736-81c0-9be562dae777"));
            }

            var task = await Task.WhenAll(tasks);

            string instanceId = "qwerty";

            if(task[0].StatusCode==System.Net.HttpStatusCode.Accepted)
            {
                while (true)
                {
                    await Task.Delay(2000);

                    string content = await task[0].Content.ReadAsStringAsync();
                    var res = JsonSerializer.Deserialize<OrchResult>(content);
                    var res2 = await HttpClient.GetAsync(res.statusQueryGetUri);
                    instanceId = res.id;

                    if (res2.StatusCode == System.Net.HttpStatusCode.OK)
                        break;
                }
            }

            var log = await LogReader.GetOrchLog(workflowName);

            Assert.IsTrue(log.FindIndex(i=>i.OrchestrationId.Equals(instanceId))>=0);

            var steps = await LogReader.GetStepsLog(workflowName, instanceId);

            var s = steps.OrderBy(e => e.EndDate).ToList();
            
            Assert.IsTrue(s[0].StepNumber == 1);
            
            if(s[1].StepNumber==2)
                Assert.IsTrue(s[2].StepNumber==4);
            else
            {
                Assert.IsTrue(s[1].StepNumber == 4);
                Assert.IsTrue(s[2].StepNumber == 2);
            }

            Assert.IsTrue(s[3].StepNumber == 5);
            Assert.IsTrue(s[4].StepNumber == 3);

            if (s[5].StepNumber == 6)
                Assert.IsTrue(s[6].StepNumber == 7);
            else
            {
                Assert.IsTrue(s[6].StepNumber == 6);
                Assert.IsTrue(s[5].StepNumber == 7);
            }

            Assert.IsTrue(s[7].StepNumber == 8);
        }
    }

    public class OrchResult
    {
        public string id { get; set; }
        public string purgeHistoryDeleteUri { get; set; }
        public string sendEventPostUri { get; set; }
        public string statusQueryGetUri { get; set; }
        public string terminatePostUri { get; set; }
    }
}