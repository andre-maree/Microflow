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
            List<Step> workflow = TestWorkflowHelper.CreateTestWorkflow_10StepsParallel();

            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(workflow);

            int loop = 1;
            string globalKey = Guid.NewGuid().ToString();

            microflow.workflow.Step(14).WaitForAllParents = false;

            // Upsert
            bool successUpsert = await TestWorkflowHelper.UpsertWorkFlow(microflow.workflow);

            Assert.IsTrue(successUpsert);

            // start the upserted Microflow
            (string instanceId, string statusUrl) startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

            List<Microflow.MicroflowTableModels.LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(startResult.instanceId)) >= 0);

            List<Microflow.MicroflowTableModels.LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, startResult.instanceId);

            List<Microflow.MicroflowTableModels.LogStepEntity> sorted = steps.OrderBy(e => e.EndDate).ToList();

            Assert.IsTrue(sorted[0].StepNumber == 1);

            if (sorted[1].StepNumber == 2)
                Assert.IsTrue(sorted[2].StepNumber == 3);
            else
            {
                Assert.IsTrue(sorted[1].StepNumber == 3);
                Assert.IsTrue(sorted[2].StepNumber == 2);
            }

            // with WaitForAllParents = true, step 14 will execute once when all parents are completed
            // with WaitForAllParents = false, step 14 will execute each time a parent completes
            Assert.IsTrue(sorted.Count(n => n.StepNumber == 14) == 10);
        }
    }
}