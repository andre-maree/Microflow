using MicroflowModels;
using MicroflowSDK;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MicroflowTest
{
    [TestClass]
    public class Test3_Webhooks
    {
        [TestMethod]
        public async Task BasicWebhook()
        {
            List<Step> workflow = TestWorkflowHelper.CreateTestWorkflow_SimpleSteps();

            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(workflow);

            int loop = 1;
            string globalKey = Guid.NewGuid().ToString();

            microflow.workflow.Step(2).WebhookId = Guid.NewGuid().ToString();
            microflow.workflow.Step(2).EnableWebhook = true;

            // Upsert
            bool successUpsert = await WorkflowManager.UpsertWorkFlow(microflow.workflow, TestWorkflowHelper.BaseUrl);

            Assert.IsTrue(successUpsert);

            // start the upserted Microflow
            HttpResponseMessage startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

            // Emulator will reply on the webhook
            if (!TestWorkflowHelper.UseEmulator)
            {
                while (true)
                {
                    await Task.Delay(2000);

                    //HttpResponseMessage webhookcall = await TestWorkflowHelper.HttpClient.GetAsync(
                    //    $"{TestWorkflowHelper.BaseUrl}/getwebhooks/{microflow.workflowName}/{microflow.workflow.Step(2).WebhookId}/{microflow.workflow.Step(2).StepNumber}");
                    HttpResponseMessage webhookcall = await TestWorkflowHelper.HttpClient.GetAsync($"{TestWorkflowHelper.BaseUrl}/webhooks/{microflow.workflow.Step(2).WebhookId}");

                    // if the callout sent out a webhookid externally, and the events is not created yet, then a 202 will always return
                    if (webhookcall.StatusCode == System.Net.HttpStatusCode.Accepted || webhookcall.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        continue;
                    }
                    else if (webhookcall.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        break;
                    }
                }
            }

            string instanceId = await WorkflowManager.WaitForWorkflowCompleted(startResult);

            List<Microflow.MicroflowTableModels.LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(instanceId)) >= 0);

            List<Microflow.MicroflowTableModels.LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, instanceId);

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

            Assert.IsTrue(s.Count == 4);
        }

        [TestMethod]
        public async Task WebhookWithCallbackActionAndSubStepsIfElse()
        {
            List<Step> workflow = TestWorkflowHelper.CreateTestWorkflow_SimpleSteps();

            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(workflow);

            // Emulator will reply on the webhook, set /approve
            if (TestWorkflowHelper.UseEmulator)
            {
                TestWorkflowHelper.SetEmulatorWebhookAction(microflow.workflow);
            }

            int loop = 1;
            string globalKey = Guid.NewGuid().ToString();

            string webhookId = Guid.NewGuid().ToString();// "mywebhook";

            microflow.workflow.Step(1).WebhookId = webhookId;
            microflow.workflow.Step(1).EnableWebhook = true;
            microflow.workflow.Step(1).WebhookSubStepsMapping = new();
            microflow.workflow.Step(1).WebhookSubStepsMapping.Add(new()
            {
                WebhookAction = "decline",
                SubStepsToRunForAction = new List<int>() { 2 }
            });
            microflow.workflow.Step(1).WebhookSubStepsMapping.Add(new()
            {
                WebhookAction = "approve",
                SubStepsToRunForAction = new List<int>() { 3 }
            });
            microflow.workflow.Step(4).WaitForAllParents = false;

            // Upsert
            bool successUpsert = await WorkflowManager.UpsertWorkFlow(microflow.workflow, TestWorkflowHelper.BaseUrl);

            Assert.IsTrue(successUpsert);

            // start the upserted Microflow
            HttpResponseMessage startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

            // Emulator will reply on the webhook
            if (!TestWorkflowHelper.UseEmulator)
            {
                while (true)
                {
                    await Task.Delay(2000);

                    HttpResponseMessage webhookcall = await TestWorkflowHelper.HttpClient.GetAsync($"{TestWorkflowHelper.BaseUrl}/webhooks/{webhookId}/approve");

                    // if the callout sent out a webhookid externally, and the events is not created yet, then a 202 will always return
                    if (webhookcall.StatusCode == System.Net.HttpStatusCode.Accepted || webhookcall.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        continue;
                    }
                    else if (webhookcall.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        break;
                    }
                }
            }

            string instanceId = await WorkflowManager.WaitForWorkflowCompleted(startResult);

            List<Microflow.MicroflowTableModels.LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(instanceId)) >= 0);

            List<Microflow.MicroflowTableModels.LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, instanceId);

            List<Microflow.MicroflowTableModels.LogStepEntity> s = steps.OrderBy(e => e.EndDate).ToList();

            Assert.IsTrue(s[0].StepNumber == 1);
            Assert.IsTrue(s[1].StepNumber == 3);
            Assert.IsTrue(s[2].StepNumber == 4);

            Assert.IsTrue(s.Count == 3);
        }
    }
}