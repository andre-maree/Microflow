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
    public class Test5_WaitForAllParents
    {
        [TestMethod]
        public async Task WaitForAllParents()
        {
            // create a workflow with parent step 1, two child steps 2 and 3, 2 with five children and 3 with five children,
            // and step 14 as the child of the ten children of 2 and 3
            List<Step> workflow = TestWorkflowHelper.CreateTestWorkflow_10StepsParallel();

            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(workflow);

            int loop = 1;
            string globalKey = Guid.NewGuid().ToString();

            microflow.workflow.Step(14).WaitForAllParents = false;

            // Upsert
            bool successUpsert = await WorkflowManager.UpsertWorkFlow(microflow.workflow, TestWorkflowHelper.BaseUrl);

            Assert.IsTrue(successUpsert);

            // start the upserted Microflow
            HttpResponseMessage startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

            string instanceId = await WorkflowManager.WaitForWorkflowCompleted(startResult);

            List<Microflow.MicroflowTableModels.LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, instanceId);

            List<Microflow.MicroflowTableModels.LogStepEntity> sorted = steps.OrderBy(e => e.EndDate).ToList();

            List<Microflow.MicroflowTableModels.LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(instanceId)) >= 0);

            Assert.IsTrue(sorted[0].StepNumber == 1);

            Assert.IsTrue(sorted[1].StepNumber == 2 || sorted[1].StepNumber == 3);

            Assert.IsTrue(sorted.Count() == 23);

            // with WaitForAllParents = true, step 14 will execute once when all parents are completed
            // with WaitForAllParents = false, step 14 will execute each time a parent completes
            Assert.IsTrue(sorted.Count(n => n.StepNumber == 14) == 10);
        }
    }
}