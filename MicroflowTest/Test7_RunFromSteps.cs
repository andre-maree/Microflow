using Microflow.MicroflowTableModels;
using MicroflowModels;
using MicroflowSDK;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace MicroflowTest
{
    [TestClass]
    public class Test7_RunFromSteps
    {
        [TestMethod]
        public async Task RunFromSteps()
        {
            string workflowName = await RunBasicWorkflow();

            HttpResponseMessage runcall = await TestWorkflowHelper.HttpClient.PostAsJsonAsync<List<int>>($"{TestWorkflowHelper.BaseUrl}/RunFromSteps/{workflowName}", new() { 2, 3 });

            //// CHECK RESULTS //// stepnumber 1 is now not in the log
            string result = await runcall.Content.ReadAsStringAsync();
            OrchResult orchResult = JsonConvert.DeserializeObject<OrchResult>(result);

            while (true)
            {
                await Task.Delay(2000);

                HttpResponseMessage res = await TestWorkflowHelper.HttpClient.GetAsync(orchResult.statusQueryGetUri);

                if (res.StatusCode == System.Net.HttpStatusCode.OK)
                    break;
            }

            // get the orchestration log to check the results
            List<LogOrchestrationEntity> log = await LogReader.GetOrchLog(workflowName);

            // check that the orchestraion id is logged 
            Assert.IsTrue(log.Single(i => i.OrchestrationId.Equals(orchResult.id) && i.RowKey.Contains("RunFromSteps")) != null);

            // get the steps log to check the results
            List<LogStepEntity> steps = await LogReader.GetStepsLog(workflowName, orchResult.id);

            List<LogStepEntity> sortedSteps = steps.OrderBy(e => e.EndDate).ToList();

            if (sortedSteps[0].StepNumber == 2)
                Assert.IsTrue(sortedSteps[1].StepNumber == 3);
            else
            {
                Assert.IsTrue(sortedSteps[0].StepNumber == 3);
                Assert.IsTrue(sortedSteps[1].StepNumber == 2);
            }

            Assert.IsTrue(sortedSteps[2].StepNumber == 4);

            Assert.IsTrue(sortedSteps.Count == 3);
        }

        private static async Task<string> RunBasicWorkflow()
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
            (string instanceId, string statusUrl) startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey);

            //// CHECK RESULTS ////

            // get the orchestration log to check the results
            List<LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            // check that the orchestraion id is logged
            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(startResult.instanceId)) >= 0);

            // get the steps log to check the results
            List<LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, startResult.instanceId);

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

            return microflow.workflowName;
        }
    }
}