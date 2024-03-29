using Microflow.MicroflowTableModels;
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
    public class Test1_WorkflowExecution
    {
        [TestMethod]
        // https://github.com/andre-maree/Microflow/wiki/Unit-Tests#getstartedworkflow
        public async Task GetStartedWorkflow()
        {
            // create a simple workflow with parent step 1, subling children step 2 and 3, and child of 2 and 3 step 4
            // siblings steps 2 and 3 runs in parallel
            List<Step> stepsList = TestWorkflowHelper.CreateTestWorkflow_SimpleSteps();

            // create Microflow with the created workflow
            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(stepsList, passThroughParams: false);

            // set loop to 1, how many time the workflow will execute
            int loop = 1;

            // set the global key if needed
            // the global key is needed to group related workflows and control the group
            string globalKey = Guid.NewGuid().ToString();

            // upsert Microflow json
            //string json = JsonSerializer.Serialize(microflow.workflow);
            bool successUpsert = await WorkflowManager.UpsertWorkFlow(microflow.workflow, TestWorkflowHelper.BaseUrl);

            Assert.IsTrue(successUpsert);

            // start the upserted Microflow
            HttpResponseMessage startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

            string instanceId = await WorkflowManager.WaitForWorkflowCompleted(startResult);

            //// CHECK RESULTS ////

            // get the orchestration log to check the results
            List<LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            // check that the orchestraion id is logged
            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(instanceId)) >= 0);

            // get the steps log to check the results
            List<LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, instanceId);

            List<LogStepEntity> sortedSteps = steps.OrderBy(e => e.EndDate).ToList();

            Assert.IsTrue(sortedSteps[0].StepNumber == 1);

            if (sortedSteps[1].StepNumber == 2)
                Assert.IsTrue(sortedSteps[2].StepNumber == 3);
            else
            {
                Assert.IsTrue(sortedSteps[1].StepNumber == 3);
                Assert.IsTrue(sortedSteps[2].StepNumber == 2);
            }

            Assert.IsTrue(sortedSteps[3].StepNumber == 4);

            Assert.IsTrue(sortedSteps.Count == 4);
        }

        [TestMethod]
        // https://github.com/andre-maree/Microflow/wiki/Unit-Tests#complexworkflow
        public async Task ComplexWorkflow()
        {
            for (int i = 0; i < 1; i++)
            {
                // create the complex 8 step workflow
                List<Step> workflow = TestWorkflowHelper.CreateTestWorkflow_Complex1();

                // create Microflow with the created workflow
                (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(workflow);

                List<Task<HttpResponseMessage>> tasks = new();

                // set loop to 1
                int loop = 1;

                // set the global key if needed
                string globalKey = Guid.NewGuid().ToString();

                // upsert Microflow json
                bool successUpsert = await WorkflowManager.UpsertWorkFlow(microflow.workflow, TestWorkflowHelper.BaseUrl);

                Assert.IsTrue(successUpsert);

                // start the upserted Microflow
                HttpResponseMessage startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

                string instanceId = await WorkflowManager.WaitForWorkflowCompleted(startResult);

                //// CHECK RESULTS ////

                // get the orchestration log to check the results
                List<LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

                Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(instanceId)) >= 0);

                // get the steps log to check the results
                List<LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, instanceId);

                List<LogStepEntity> sortedSteps = steps.OrderBy(e => e.EndDate).ToList();

                Assert.IsTrue(sortedSteps[0].StepNumber == 1);

                // simple parents checks
                var step2 = sortedSteps.Find(f => f.StepNumber == 2);
                var step3 = sortedSteps.Find(f => f.StepNumber == 3);
                var step4 = sortedSteps.Find(f => f.StepNumber == 4);
                var step5 = sortedSteps.Find(f => f.StepNumber == 5);
                var step6 = sortedSteps.Find(f => f.StepNumber == 6);
                var step7 = sortedSteps.Find(f => f.StepNumber == 7);

                var parents = sortedSteps.FindAll(s => s.EndDate < step2.StartDate);
                Assert.IsTrue(parents.Count == 1 && parents[0].StepNumber == 1);

                parents = sortedSteps.FindAll(s => s.EndDate < step3.StartDate);
                Assert.IsTrue(parents.Contains(sortedSteps[0]) && parents.Contains(step5));

                parents = sortedSteps.FindAll(s => s.EndDate < step4.StartDate);
                Assert.IsTrue(parents.Contains(sortedSteps[0]));

                parents = sortedSteps.FindAll(s => s.EndDate < step5.StartDate);
                Assert.IsTrue(parents.Contains(step2));

                parents = sortedSteps.FindAll(s => s.EndDate < step6.StartDate);
                Assert.IsTrue(parents.Contains(step4) && parents.Contains(step5));

                parents = sortedSteps.FindAll(s => s.EndDate < step7.StartDate);
                Assert.IsTrue(parents.Contains(step3));

                Assert.IsTrue(sortedSteps[7].StepNumber == 8);

                Assert.IsTrue(sortedSteps.Count == 8);
            }
        }

        [TestMethod]
        // This is the GetStartedWorkflow() running twice in parallel
        public async Task GetStartedWorkflowWithParallelRunnibgWorkflows()
        {
            // create a simple workflow with parent step 1, subling children step 2 and 3, and child of 2 and 3 step 4
            // siblings steps 2 and 3 runs in parallel
            List<Step> stepsList = TestWorkflowHelper.CreateTestWorkflow_SimpleSteps();

            // create Microflow with the created workflow
            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(stepsList);

            // set loop to 1, how many time the workflow will execute
            int loop = 1;

            // set the global key if needed
            // the global key is needed to group related workflows and control the group
            string globalKey = Guid.NewGuid().ToString();

            // upsert Microflow json
            //string json = JsonSerializer.Serialize(microflow.workflow);
            bool successUpsert = await WorkflowManager.UpsertWorkFlow(microflow.workflow, TestWorkflowHelper.BaseUrl);

            Assert.IsTrue(successUpsert);

            // start the upserted workflow in parallel x2 instances
            var startResult1Task = TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);
            var startResult2Task = TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);
            HttpResponseMessage startResult1 = await startResult1Task;
            HttpResponseMessage startResult2 = await startResult2Task;

            string instanceId1 = await WorkflowManager.WaitForWorkflowCompleted(startResult1);
            string instanceId2 = await WorkflowManager.WaitForWorkflowCompleted(startResult2);

            //// CHECK RESULTS ////

            Assert.IsTrue(!instanceId1.Equals(instanceId2));

            // get the orchestration log to check the results
            List<LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);


            //////////////////// run 1`s checks ////////////////////////////
            ///
            List<LogStepEntity> sortedSteps1 = await WorkflowInstance1Checks(microflow, instanceId1, log);

            //////////////////// run 2`s checks ////////////////////////////
            ///
            List<LogStepEntity> sortedSteps2 = await WorkflowInstance2Checks(microflow, instanceId2, log);


            /////////////////// check that these 2 actually ran in parallel ////////////////////////////
            /// run 1 step 4 ended after run 2 step 1 started
            /// run 2 step 4 ended after run 1 step 1 started
            Assert.IsTrue(sortedSteps1[3].EndDate > sortedSteps2[0].StartDate);
            Assert.IsTrue(sortedSteps2[3].EndDate > sortedSteps1[0].StartDate);
        }

        /// <summary>
        /// Inserts a workflow and starts it - skips workflow and global states, deletes, workflow save, create tables
        /// This will update the entire entity, so if its not the same steps then an unexpacted issue can occur
        /// </summary>
        // http://localhost:7071/microflow/v1/QuickInsertAndStartWorkflow/{workflowName}/{instanceId}/{globalKey?}
        [TestMethod]
        // https://github.com/andre-maree/Microflow/wiki/Unit-Tests#getstartedworkflow
        public async Task QuickInsertAndStartTest()
        {
            // create a simple workflow with parent step 1, subling children step 2 and 3, and child of 2 and 3 step 4
            // siblings steps 2 and 3 runs in parallel
            List<Step> stepsList = TestWorkflowHelper.CreateTestWorkflow_SimpleSteps();

            // create Microflow with the created workflow
            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(stepsList, passThroughParams: false);

            // set loop to 1, how many time the workflow will execute
            int loop = 1;

            // set the global key if needed
            // the global key is needIded to group related workflows and control the group
            string globalKey = Guid.NewGuid().ToString();

            // upsert Microflow json
            //string json = JsonSerializer.Serialize(microflow.workflow);
            HttpResponseMessage insertAndStart = await WorkflowManager.QuickInsertAndStartWorkFlow(microflow.workflow, TestWorkflowHelper.BaseUrl);

            string instanceId = await WorkflowManager.WaitForWorkflowCompleted(insertAndStart);

            //// start the upserted Microflow
            //(string instanceId, string statusUrl) startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

            ////// CHECK RESULTS ////

            //// get the orchestration log to check the results
            List<LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            // check that the orchestraion id is logged
            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(instanceId)) >= 0);

            // get the steps log to check the results
            List<LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, instanceId);

            List<LogStepEntity> sortedSteps = steps.OrderBy(e => e.EndDate).ToList();

            Assert.IsTrue(sortedSteps[0].StepNumber == 1);

            if (sortedSteps[1].StepNumber == 2)
                Assert.IsTrue(sortedSteps[2].StepNumber == 3);
            else
            {
                Assert.IsTrue(sortedSteps[1].StepNumber == 3);
                Assert.IsTrue(sortedSteps[2].StepNumber == 2);
            }

            Assert.IsTrue(sortedSteps[3].StepNumber == 4);

            Assert.IsTrue(sortedSteps.Count == 4);
        }

        private static async Task<List<LogStepEntity>> WorkflowInstance1Checks((MicroflowModels.Microflow workflow, string workflowName) microflow, string instanceId, List<LogOrchestrationEntity> log)
        {
            // check that the orchestraion id is logged
            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(instanceId)) >= 0);

            // get the steps log to check the results
            List<LogStepEntity> steps1 = await LogReader.GetStepsLog(microflow.workflowName, instanceId);

            List<LogStepEntity> sortedSteps1 = steps1.OrderBy(e => e.EndDate).ToList();

            Assert.IsTrue(sortedSteps1[0].StepNumber == 1);

            if (sortedSteps1[1].StepNumber == 2)
                Assert.IsTrue(sortedSteps1[2].StepNumber == 3);
            else
            {
                Assert.IsTrue(sortedSteps1[1].StepNumber == 3);
                Assert.IsTrue(sortedSteps1[2].StepNumber == 2);
            }

            Assert.IsTrue(sortedSteps1[3].StepNumber == 4);

            Assert.IsTrue(sortedSteps1.Count == 4);
            return sortedSteps1;
        }

        private static async Task<List<LogStepEntity>> WorkflowInstance2Checks((MicroflowModels.Microflow workflow, string workflowName) microflow, string instanceId, List<LogOrchestrationEntity> log)
        {
            // check that the orchestraion id is logged for other run
            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(instanceId)) >= 0);

            // get the steps log to check the results
            List<LogStepEntity> steps2 = await LogReader.GetStepsLog(microflow.workflowName, instanceId);

            List<LogStepEntity> sortedSteps2 = steps2.OrderBy(e => e.EndDate).ToList();

            Assert.IsTrue(sortedSteps2[0].StepNumber == 1);

            if (sortedSteps2[1].StepNumber == 2)
                Assert.IsTrue(sortedSteps2[2].StepNumber == 3);
            else
            {
                Assert.IsTrue(sortedSteps2[1].StepNumber == 3);
                Assert.IsTrue(sortedSteps2[2].StepNumber == 2);
            }

            Assert.IsTrue(sortedSteps2[3].StepNumber == 4);

            Assert.IsTrue(sortedSteps2.Count == 4);
            return sortedSteps2;
        }
    }
}