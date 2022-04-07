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

            var ff = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(jsonString);

            var workflow = TestWorkflows.CreateTestWorkflow_Complex1();

            var microflow = new Microflow()
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

            microflow.Step(2).WebhookAction = "warra";

            // Upsert
            var result = await HttpClient.PostAsJsonAsync(baseUrl + "/UpsertWorkflow/", microflow, new JsonSerializerOptions(JsonSerializerDefaults.General));

            Assert.IsTrue(result.StatusCode == System.Net.HttpStatusCode.OK);

            for (int i = 0; i < 1; i++)
            {
                //await Task.Delay(200);
                tasks.Add(HttpClient.GetAsync(baseUrl + $"/Start/{workflowName}?globalkey={globalKey}&loop={loop}"));
                //tasks.Add(HttpClient.GetAsync(baseUrl + $"/MicroflowStart/{project.ProjectName}/33306875-9c81-4736-81c0-9be562dae777"));
            }

            _ = await Task.WhenAll(tasks);

            Assert.IsFalse(tasks.Exists(e => e.Result.StatusCode != System.Net.HttpStatusCode.Accepted));

            var log = await LogReader.GetOrchLog(workflowName);
        }
    }
}