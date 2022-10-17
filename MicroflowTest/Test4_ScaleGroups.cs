using Microflow.MicroflowTableModels;
using MicroflowModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MicroflowTest
{
    [TestClass]
    public class Test4_ScaleGroups
    {
        string scaleGroupId = "mytestgroup";

        [TestMethod]
        public async Task MaxInstanceCountForScaleGroupTo10()
        {
            // create a simple workflow with parent step 1, subling children step 2 and 3, and child of 2 and 3 step 4
            // siblings steps 2 and 3 runs in parallel
            List<Step> workflow = TestWorkflowHelper.CreateTestWorkflow_10StepsParallel();

            // create Microflow with the created workflow
            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(workflow);

            // set loop to 1, how many time the workflow will execute
            int loop = 1;

            // set the global key if needed
            // the global key is needed to group related workflows and control the group
            string globalKey = Guid.NewGuid().ToString();

            microflow.workflow.Steps.Where(s => s.StepNumber > 3 && s.StepNumber < 14).ToList().ForEach(x => x.ScaleGroupId = scaleGroupId);

            await UpsertWorkflow(microflow);

            await TestWorkflowHelper.SetScaleGroupMax(10, scaleGroupId);

            // start the upserted Microflow
            (string instanceId, string statusUrl) startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

            await TestNoScaleGroupWithMax10(microflow, startResult);
        }

        [TestMethod]
        public async Task MaxInstanceCountForScaleGroupTo1()
        {
            // create a simple workflow with parent step 1, subling children step 2 and 3, and child of 2 and 3 step 4
            // siblings steps 2 and 3 runs in parallel
            List<Step> workflow = TestWorkflowHelper.CreateTestWorkflow_10StepsParallel();

            // create Microflow with the created workflow
            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(workflow);

            // set loop to 1, how many time the workflow will execute
            int loop = 1;

            // set the global key if needed
            // the global key is needed to group related workflows and control the group
            string globalKey = Guid.NewGuid().ToString();

            microflow.workflow.Steps.Where(s => s.StepNumber > 3 && s.StepNumber < 14).ToList().ForEach(x => x.ScaleGroupId = scaleGroupId);

            await UpsertWorkflow(microflow);

            await TestWorkflowHelper.SetScaleGroupMax(1, scaleGroupId);

            // start the upserted Microflow
            (string instanceId, string statusUrl) startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

            await TestNoScaleGroupWithMax1(microflow, startResult);
        }

            private static async Task TestNoScaleGroupWithMax1((MicroflowModels.Microflow workflow, string workflowName) microflow, (string instanceId, string statusUrl) startResult)
        {
            //// CHECK RESULTS ////

            // get the orchestration log to check the results
            List<LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(startResult.instanceId)) >= 0);

            // get the steps log to check the results
            List<LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, startResult.instanceId);

            List<LogStepEntity> sortedSteps = TestBasicFlow(steps);

            IEnumerable<LogStepEntity> parallelSteps = sortedSteps.Where(s => s.StepNumber > 3 && s.StepNumber < 14);
            int count = 1;

            foreach (LogStepEntity paraStep in parallelSteps)
            {
                // these are now in sequence since the the scalegroup max instance count is 1
                // count the other step`s with start dates before this step`s end date
                if (parallelSteps.Count(s => s.StartDate < paraStep.EndDate) != count)
                {
                    Assert.Fail();
                    break;
                }

                // now 1 more other step has a start date before the next step`s end date
                count++;
            }
        }

        private static async Task TestNoScaleGroupWithMax10((MicroflowModels.Microflow workflow, string workflowName) microflow, (string instanceId, string statusUrl) startResult)
        {
            //// CHECK RESULTS ////

            // get the orchestration log to check the results
            List<LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(startResult.instanceId)) >= 0);

            // get the steps log to check the results
            List<LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, startResult.instanceId);

            List<LogStepEntity> sortedSteps = TestBasicFlow(steps);

            IEnumerable<LogStepEntity> parallelSteps = sortedSteps.Where(s => s.StepNumber > 3 && s.StepNumber < 14);
            bool foundOverlap = false;

            foreach (LogStepEntity paraStep in parallelSteps)
            {
                if (parallelSteps.Count(s => s.StartDate < paraStep.EndDate) > 0)
                {
                    foundOverlap = true;
                    break;
                }
            }

            Assert.IsTrue(foundOverlap);
        }

        private static List<LogStepEntity> TestBasicFlow(List<LogStepEntity> steps)
        {
            List<LogStepEntity> sortedSteps = steps.OrderBy(e => e.EndDate).ToList();

            Assert.IsTrue(sortedSteps[0].StepNumber == 1);

            if (sortedSteps[1].StepNumber == 2)
                Assert.IsTrue(sortedSteps[2].StepNumber == 3);
            else
            {
                Assert.IsTrue(sortedSteps[1].StepNumber == 3);
                Assert.IsTrue(sortedSteps[2].StepNumber == 2);
            }

            Assert.IsTrue(sortedSteps.Count == 14);
            Assert.IsTrue(sortedSteps[13].StepNumber == 14);

            return sortedSteps;
        }

        private static async Task UpsertWorkflow((MicroflowModels.Microflow workflow, string workflowName) microflow)
        {
            // upsert Microflow json
            //string json = JsonSerializer.Serialize(microflow.workflow);
            bool successUpsert = await TestWorkflowHelper.UpsertWorkFlow(microflow.workflow);

            Assert.IsTrue(successUpsert);
        }
    }
}