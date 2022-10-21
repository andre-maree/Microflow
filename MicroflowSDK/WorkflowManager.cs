using MicroflowModels;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace MicroflowSDK
{
    public class PassThroughParams
    {
        public bool WorkflowName { get; set; } = true;
        public bool MainOrchestrationId { get; set; } = true;
        public bool SubOrchestrationId { get; set; } = true;
        public bool WebhookId { get; set; } = true;
        public bool RunId { get; set; } = true;
        public bool StepNumber { get; set; } = true;
        public bool GlobalKey { get; set; } = true;
        public bool StepId { get; set; } = true;
    }

    public static class WorkflowManager
    {
        public static HttpClient HttpClient = new();

        public static Step Step(this Microflow microFlow, int stepNumber) => microFlow.Steps.First(s => s.StepNumber == stepNumber);

        public static Step StepNumber(this List<Step> steps, int stepNumber) => steps.First(s => s.StepNumber == stepNumber);

        public static List<Step> CreateSteps(int count, int fromId, string defaultCalloutURI = "")
        {
            List<Step> stepsList = new();

            for (; fromId <= count; fromId++)
            {
                Step step = new(fromId, defaultCalloutURI, "myStep " + fromId);
                stepsList.Add(step);
            }

            return stepsList;
        }

        public static async Task<bool> UpsertWorkFlow(Microflow workflow, string baseUrl)
        {
            // Upsert
            HttpResponseMessage result = await HttpClient.PostAsJsonAsync(baseUrl + "/UpsertWorkflow/", workflow, new JsonSerializerOptions(JsonSerializerDefaults.General));

            if (result.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return true;
            }

            return false;
        }

        public static async Task<HttpResponseMessage> QuickInsertAndStartWorkFlow(Microflow workflow, string baseUrl)
        {
            // Upsert and start
            return await HttpClient.PostAsJsonAsync(baseUrl + $"/QuickInsertAndStartWorkflow/{workflow.WorkflowName}@{workflow.WorkflowVersion}", workflow, new JsonSerializerOptions(JsonSerializerDefaults.General));
        }

        public static async Task<string> WaitForWorkflowCompleted(HttpResponseMessage resp)
        {
            OrchResult? res = JsonSerializer.Deserialize<OrchResult>(await resp.Content.ReadAsStringAsync());

            string res2result = "";

            while (!res2result.Contains("\"runtimeStatus\":\"Completed\""))
            {
                await Task.Delay(2000);

                HttpResponseMessage res2 = await HttpClient.GetAsync(res.statusQueryGetUri);
                res2result = await res2.Content.ReadAsStringAsync();

                if (res2result.Contains("\"runtimeStatus\":\"Completed\""))
                    break;
            }

            return res.id;
        }
    }
    public class OrchResult
    {
        public string id { get; set; }
        public string purgeHistoryDeleteUri { get; set; }
        public string sendEventPostUri { get; set; }
        public string statusQueryGetUri { get; set; }
        public string terminatePostUri { get; set; }
    }
}
