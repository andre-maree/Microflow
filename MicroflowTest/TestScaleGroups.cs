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
    public class TestScaleGroups
    {
        [TestMethod]
        public async Task SetMaxInstanceCountForScaleGroup()
        {
            // create a simple workflow with parent step 1, subling children step 2 and 3, and child of 2 and 3 step 4
            // siblings steps 2 and 3 runs in parallel
            List<Step> workflow = TestWorkflowHelper.CreateTestWorkflow_SimpleSteps();

            // create Microflow with the created workflow
            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(workflow);

            List<Task<HttpResponseMessage>> tasks = new();

            // set loop to 1, how many time the workflow will execute
            int loop = 1;

            // set the global key if needed
            // the global key is needed to group related workflows and control the group
            string globalKey = Guid.NewGuid().ToString();

            // set scale group
            HttpResponseMessage scaleGroupSet = await ScaleGroupsManager.SetMaxInstanceCountForScaleGroup("mytestgroup", 1, TestWorkflowHelper.BaseUrl, TestWorkflowHelper.HttpClient);

            Assert.IsTrue(scaleGroupSet.StatusCode == System.Net.HttpStatusCode.OK);

            Dictionary<string, int> scaleGroupGet = await ScaleGroupsManager.GetScaleGroupsWithMaxInstanceCounts("mytestgroup", TestWorkflowHelper.BaseUrl, TestWorkflowHelper.HttpClient);
            Assert.IsTrue(scaleGroupGet["mytestgroup"]==1);
            // upsert Microflow json
            //string json = JsonSerializer.Serialize(microflow.workflow);
            bool successUpsert = await TestWorkflowHelper.UpsertWorkFlow(microflow.workflow);

            Assert.IsTrue(successUpsert);

            // start the upserted Microflow
            (string instanceId, string statusUrl) startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey, true);

            //// CHECK RESULTS ////

            // get the orchestration log to check the results
            var log = await LogReader.GetOrchLog(microflow.workflowName);

            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(startResult.instanceId)) >= 0);

            // get the steps log to check the results
            var steps = await LogReader.GetStepsLog(microflow.workflowName, startResult.instanceId);

            var sortedSteps = steps.OrderBy(e => e.EndDate).ToList();

            Assert.IsTrue(sortedSteps[0].StepNumber == 1);

            if (sortedSteps[1].StepNumber == 2)
                Assert.IsTrue(sortedSteps[2].StepNumber == 3);
            else
            {
                Assert.IsTrue(sortedSteps[1].StepNumber == 3);
                Assert.IsTrue(sortedSteps[2].StepNumber == 2);
            }

            Assert.IsTrue(sortedSteps[3].StepNumber == 4);
        }
    }
}