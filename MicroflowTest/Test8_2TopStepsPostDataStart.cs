using MicroflowModels;
using MicroflowSDK;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MicroflowTest
{
    public class PostTestObject
    {
        public string Name { get; set; } = "Andre";
        public int Id { get; set; } = 1;
    }

    [TestClass]
    public class Test8_2TopStepsPostDataStart
    {
        [TestMethod]
        public async Task TwoTopStepsPostDataStart()
        {
            // create a simple workflow top steps 1 and 2, with a child step 3, and step 3 with child step 4
            List<Step> stepsList = TestWorkflowHelper.CreateTestWorkflow_2TopSteps();
            stepsList.ForEach(x=>x.IsHttpGet = false);

            // create Microflow with the created workflow
            (MicroflowModels.Microflow workflow, string workflowName) microflow = TestWorkflowHelper.CreateMicroflow(stepsList, isHttpGet: false);

            // set loop to 1, how many time the workflow will execute
            int loop = 1;

            // set the global key if needed
            // the global key is needed to group related workflows and control the group
            string globalKey = Guid.NewGuid().ToString();

            // upsert Microflow json
            //string json = JsonSerializer.Serialize(microflow.workflow);
            bool successUpsert = await WorkflowManager.UpsertWorkFlow(microflow.workflow, TestWorkflowHelper.BaseUrl);

            Assert.IsTrue(successUpsert);

            string input = JsonConvert.SerializeObject(new PostTestObject());

            // start the upserted Microflow
            HttpResponseMessage startResult = await TestWorkflowHelper.StartMicroflow(microflow, loop, globalKey, input);

            string instanceId = await WorkflowManager.WaitForWorkflowCompleted(startResult);

            //// CHECK RESULTS ////
            ///
            // get the steps log to check the results
            List<Microflow.MicroflowTableModels.LogStepEntity> steps = await LogReader.GetStepsLog(microflow.workflowName, instanceId);

            // get the orchestration log to check the results
            List<Microflow.MicroflowTableModels.LogOrchestrationEntity> log = await LogReader.GetOrchLog(microflow.workflowName);

            // check that the orchestraion id is logged
            Assert.IsTrue(log.FindIndex(i => i.OrchestrationId.Equals(instanceId)) >= 0);

            List<Microflow.MicroflowTableModels.LogStepEntity> sortedSteps = steps.OrderBy(e => e.EndDate).ToList();

            bool step0is1 = false;

            if (sortedSteps[0].StepNumber == 1)
            {
                Assert.IsTrue(sortedSteps[1].StepNumber == 2);
                step0is1 = true;
            }
            else
            {
                Assert.IsTrue(sortedSteps[0].StepNumber == 2);
                Assert.IsTrue(sortedSteps[1].StepNumber == 1);
            }

            Assert.IsTrue(sortedSteps[2].StepNumber == 3);
            Assert.IsTrue(sortedSteps[3].StepNumber == 4);

            var arr = sortedSteps[0].PartitionKey.Split("__");

            var blobHttpRequest1Task = HttpBlobDataManager.GetHttpBlob(true, microflow.workflowName, sortedSteps[0].StepNumber, sortedSteps[0].RunId, sortedSteps[0].SubOrchestrationId);
            var blobHttpRequest2Task = HttpBlobDataManager.GetHttpBlob(true, microflow.workflowName, sortedSteps[1].StepNumber, sortedSteps[1].RunId, sortedSteps[1].SubOrchestrationId);

            await Task.WhenAll(blobHttpRequest1Task, blobHttpRequest2Task);

            var blobHttpRosponse1 = blobHttpRequest1Task.Result;
            var blobHttpRosponse2 = blobHttpRequest2Task.Result;

            if (step0is1)
            {
                string s1 = "{\"WorkflowName\":\"Unit_test_workflow@1.0\",\"MainOrchestrationId\":\"" +
                    arr[1] + "\"," +
                    "\"SubOrchestrationId\":\"" +
                    sortedSteps[0].SubOrchestrationId + "\"," +
                    "\"Webhook\":null," +
                    "\"RunId\":\"" +
                    sortedSteps[0].RunId + "\"," +
                    "\"StepNumber\":1,\"StepId\":\"myStep 1\",\"GlobalKey\":\"" +
                    sortedSteps[0].GlobalKey + "\"," +
                    "\"PostData\":\"{\\u0022Name\\u0022:\\u0022Andre\\u0022,\\u0022Id\\u0022:1}\"}";

                string s2 = "{\"WorkflowName\":\"Unit_test_workflow@1.0\",\"MainOrchestrationId\":\"" +
                    arr[1] + "\"," +
                    "\"SubOrchestrationId\":\"" +
                    sortedSteps[1].SubOrchestrationId + "\"," +
                    "\"Webhook\":null," +
                    "\"RunId\":\"" +
                    sortedSteps[1].RunId + "\"," +
                    "\"StepNumber\":2,\"StepId\":\"myStep 2\",\"GlobalKey\":\"" +
                    sortedSteps[1].GlobalKey + "\"," +
                    "\"PostData\":\"{\\u0022Name\\u0022:\\u0022Andre\\u0022,\\u0022Id\\u0022:1}\"}";

                Assert.IsTrue(blobHttpRosponse1.Equals(s1));
                Assert.IsTrue(blobHttpRosponse2.Equals(s2));
            }
            else
            {
                string s1 = "{\"WorkflowName\":\"Unit_test_workflow@1.0\",\"MainOrchestrationId\":\"" +
                    arr[1] + "\"," +
                    "\"SubOrchestrationId\":\"" +
                    sortedSteps[1].SubOrchestrationId + "\"," +
                    "\"Webhook\":null," +
                    "\"RunId\":\"" +
                    sortedSteps[1].RunId + "\"," +
                    "\"StepNumber\":1,\"StepId\":\"myStep 1\",\"GlobalKey\":\"" +
                    sortedSteps[1].GlobalKey + "\"," +
                    "\"PostData\":\"{\\u0022Name\\u0022:\\u0022Andre\\u0022,\\u0022Id\\u0022:1}\"}";

                string s2 = "{\"WorkflowName\":\"Unit_test_workflow@1.0\",\"MainOrchestrationId\":\"" +
                    arr[1] + "\"," +
                    "\"SubOrchestrationId\":\"" +
                    sortedSteps[0].SubOrchestrationId + "\"," +
                    "\"Webhook\":null," +
                    "\"RunId\":\"" +
                    sortedSteps[0].RunId + "\"," +
                    "\"StepNumber\":2,\"StepId\":\"myStep 2\",\"GlobalKey\":\"" +
                    sortedSteps[0].GlobalKey + "\"," +
                    "\"PostData\":\"{\\u0022Name\\u0022:\\u0022Andre\\u0022,\\u0022Id\\u0022:1}\"}";

                Assert.IsTrue(blobHttpRosponse1.Equals(s2));
                Assert.IsTrue(blobHttpRosponse2.Equals(s1));
            }
        }
    }
}