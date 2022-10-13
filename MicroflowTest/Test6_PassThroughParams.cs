using MicroflowModels;
using MicroflowSDK;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MicroflowTest
{
    [TestClass]
    public class Test6_PassThroughParams
    {
        [TestMethod]
        public async Task HttpGet_PassAllParams()
        {
            // create a simple workflow with parent step 1, subling children step 2 and 3, and child of 2 and 3 step 4
            // siblings steps 2 and 3 runs in parallel
            List<Step> stepsList = TestWorkflowHelper.CreateTestWorkflow_SimpleSteps();
            stepsList.RemoveRange(1, 3);
            stepsList[0].IsHttpGet = true;
            stepsList[0].SubSteps.Clear();
            stepsList[0].WebhookId = "a";
            //stepsList[3].WaitForAllParents = false;

            // create Microflow with the created workflow
            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(stepsList, true);

            // set loop to 1, how many time the workflow will execute
            int loop = 1;

            // set the global key if needed
            // the global key is needed to group related workflows and control the group
            string globalKey = Guid.NewGuid().ToString();

            // upsert Microflow json
            //string json = JsonSerializer.Serialize(microflow.workflow);
            bool successUpsert = await TestWorkflowHelper.UpsertWorkFlow(microflow.workflow);

            Assert.IsTrue(successUpsert);

            // start the upserted Microflow
            (string instanceId, string statusUrl) startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

            //// CHECK RESULTS ////
            ///
            // get the steps log to check the results
            List<Microflow.MicroflowTableModels.LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, startResult.instanceId);

            // get the orchestration log to check the results
            List<Microflow.MicroflowTableModels.LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            // check that the orchestraion id is logged
            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(startResult.instanceId)) >= 0);

            List<Microflow.MicroflowTableModels.LogStepEntity> sortedSteps = steps.OrderBy(e => e.EndDate).ToList();

            Assert.IsTrue(sortedSteps[0].StepNumber == 1);

            string blobHttpRosponse = await HttpBlobDataManager.GetHttpBlob(microflow.workflowName, sortedSteps[0].StepNumber, sortedSteps[0].RunId, sortedSteps[0].SubOrchestrationId);

            Assert.IsTrue(blobHttpRosponse.Equals("{\"success\":\"true\"}\n"));

            var arr = sortedSteps[0].PartitionKey.Split("__");

            var s = $"https://reqbin.com/echo/get/json?WorkflowName=Myflow_ClientX2@2.1&MainOrchestrationId={arr[1]}&SubOrchestrationId={sortedSteps[0].SubOrchestrationId}&WebhookId=a&RunId={sortedSteps[0].RunId}&StepNumber=1&GlobalKey={sortedSteps[0].GlobalKey}&StepId={stepsList[0].StepId}";

            Assert.IsTrue(s.Equals(sortedSteps[0].CalloutUrl));
        }
    }
}