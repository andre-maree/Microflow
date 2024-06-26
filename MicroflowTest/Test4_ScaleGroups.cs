using Microflow.MicroflowTableModels;
using MicroflowModels;
using MicroflowSDK;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
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
            // create a workflow with parent step 1, two child steps 2 and 3, 2 with five children and 3 with five children,
            // and step 14 as the child of the ten children of 2 and 3
            List<Step> workflow = TestWorkflowHelper.CreateTestWorkflow_10StepsParallel();

            // create Microflow with the created workflow
            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(workflow);

            // set loop to 1, how many time the workflow will execute
            int loop = 1;

            // set the global key if needed
            // the global key is needed to group related workflows and control the group
            string globalKey = Guid.NewGuid().ToString();

            // set the ten parallel steps 4 - 13 to the same scale group with the max instance count = 10, will overlap
            microflow.workflow.Steps.Where(s => s.StepNumber > 3 && s.StepNumber < 14).ToList().ForEach(x => x.ScaleGroupId = scaleGroupId);

            // upsert the workflow
            await UpsertWorkflow(microflow);

            // set the maximum limit of concurrent executions for the scale group id tp 10
            await TestWorkflowHelper.SetScaleGroupMax(10, scaleGroupId);

            // start the upserted Microflow
            HttpResponseMessage startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

            // wait for completion
            string instanceId = await WorkflowManager.WaitForWorkflowCompleted(startResult);

            // test that 
            await TestNoScaleGroupWithMax10(microflow, instanceId);
        }

        [TestMethod]
        public async Task MaxInstanceCountForScaleGroupTo1()
        {
            // create a workflow with parent step 1, two child steps 2 and 3, 2 with five children and 3 with five children,
            // and step 14 as the child of the ten children of 2 and 3
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

            // set the ten parallel steps 4 - 13 to the same scale group with the max instance count = 1, no overlapping
            await TestWorkflowHelper.SetScaleGroupMax(1, scaleGroupId);

            // start the upserted Microflow
            HttpResponseMessage startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

            string instanceId = await WorkflowManager.WaitForWorkflowCompleted(startResult);

            await TestNoScaleGroupWithMax1(microflow, instanceId);
        }

            private static async Task TestNoScaleGroupWithMax1((MicroflowModels.Microflow workflow, string workflowName) microflow, string instanceId)
        {
            //// CHECK RESULTS ////

            // get the orchestration log to check the results
            List<LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(instanceId)) >= 0);

            // get the steps log to check the results
            List<LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, instanceId);

            List<LogStepEntity> sortedSteps = TestBasicFlow(steps);

            IEnumerable<LogStepEntity> parallelSteps = sortedSteps.Where(s => s.StepNumber > 3 && s.StepNumber < 14);
            int count = 1;

            foreach (LogStepEntity paraStep in parallelSteps)
            {
                // these are now not overlapping since the scalegroup max instance count is 1
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

        private static async Task TestNoScaleGroupWithMax10((MicroflowModels.Microflow workflow, string workflowName) microflow, string instanceId)
        {
            //// CHECK RESULTS ////

            // get the orchestration log to check the results
            List<LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(instanceId)) >= 0);

            // get the steps log to check the results
            List<LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, instanceId);

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
            Assert.IsTrue(sortedSteps[1].StepNumber == 2 || sortedSteps[1].StepNumber == 3);
            Assert.IsTrue(sortedSteps.Count(n => n.StepNumber == 14) == 1);
            Assert.IsTrue(sortedSteps.Count == 14);
            Assert.IsTrue(sortedSteps[13].StepNumber == 14);

            return sortedSteps;
        }

        private static async Task UpsertWorkflow((MicroflowModels.Microflow workflow, string workflowName) microflow)
        {
            // upsert Microflow json
            //string json = JsonSerializer.Serialize(microflow.workflow);
            bool successUpsert = await WorkflowManager.UpsertWorkFlow(microflow.workflow, TestWorkflowHelper.BaseUrl);

            Assert.IsTrue(successUpsert);
        }
    }
}