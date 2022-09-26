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
    public class Test3_Webhooks
    {
        public static readonly HttpClient HttpClient = new HttpClient();
        private static string baseUrl = "http://localhost:7071/microflow/v1";

        [TestMethod]
        public async Task CreateTestWebhooksWorkflow()
        {
            List<Step> workflow = TestWorkflowHelper.CreateTestWorkflow_SimpleSteps();

            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(workflow);

            int loop = 1;
            string globalKey = Guid.NewGuid().ToString();

            string webhookId = $"{microflow.workflowName}@1@managerApproval@test";
            microflow.workflow.Step(1).SetWebhook("webhook", webhookId);

            // Upsert
            bool successUpsert = await TestWorkflowHelper.UpsertWorkFlow(microflow.workflow);

            Assert.IsTrue(successUpsert);

            // start the upserted Microflow
            (string instanceId, string statusUrl) startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey, false);

            bool donewebhook = false;

            while (true)
            {
                await Task.Delay(2000);

                if (!donewebhook)
                {
                    HttpResponseMessage webhookcall = await HttpClient.GetAsync("http://localhost:7071/microflow/v1/webhook/Myflow_ClientX2@2.1@1@managerApproval@test");

                    if (webhookcall.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        donewebhook = true;
                    }
                }

                HttpResponseMessage res = await HttpClient.GetAsync(startResult.statusUrl);

                if (res.StatusCode == System.Net.HttpStatusCode.OK)
                    break;
            }

            List<Microflow.MicroflowTableModels.LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(startResult.instanceId)) >= 0);

            List<Microflow.MicroflowTableModels.LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, startResult.instanceId);

            List<Microflow.MicroflowTableModels.LogStepEntity> s = steps.OrderBy(e => e.EndDate).ToList();

            Assert.IsTrue(s[0].StepNumber == 1);

            if (s[1].StepNumber == 2)
                Assert.IsTrue(s[2].StepNumber == 3);
            else
            {
                Assert.IsTrue(s[1].StepNumber == 3);
                Assert.IsTrue(s[2].StepNumber == 2);
            }

            Assert.IsTrue(s[3].StepNumber == 4);
        }
    }
}