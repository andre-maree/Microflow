using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microflow.Models;
using MicroflowModels;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microflow.Helpers
{
    public static class MicroflowProjectHelper
    {
        /// <summary>
        /// This is called from on start of workflow execution,
        /// does the looping and calls "ExecuteStep" for each top level step,
        /// by getting step -1 from table storage
        /// </summary>
        /// <returns></returns>
        public static async Task MicroflowStartProjectRun(this IDurableOrchestrationContext context, ILogger log, ProjectRun projectRun)
        {
            // do the looping
            for (int i = 1; i <= projectRun.Loop; i++)
            {
                // get the top container step from table storage (from PrepareWorkflow)
                HttpCallWithRetries httpCallWithRetries = await context.CallActivityAsync<HttpCallWithRetries>("GetStep", projectRun);

                string guid = context.NewGuid().ToString();

                projectRun.RunObject.RunId = guid;

                log.LogError($"Started Run ID {projectRun.RunObject.RunId}...");

                // pass in the current loop count so it can be used downstream/passed to the micro-services
                projectRun.CurrentLoop = i;

                List<List<int>> subSteps = JsonSerializer.Deserialize<List<List<int>>>(httpCallWithRetries.SubSteps);

                List<Task> subTasks = new List<Task>();

                foreach (List<int> step in subSteps)
                {
                    projectRun.RunObject = new RunObject() { RunId = guid, StepId = step[0] };

                    subTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", projectRun));
                }

                await Task.WhenAll(subTasks);

                log.LogError($"Run ID {guid} completed successfully...");
            }
        }

        /// <summary>
        /// Must be called at least once before a workflow creation or update,
        /// do not call this repeatedly when running multiple concurrent instances,
        /// only call this to create a new workflow or to update an existing 1
        /// Saves step meta data to table storage and read during execution
        /// </summary>
        public static async Task PrepareWorkflow(this ProjectRun projectRun, List<Step> steps)
        {
            List<List<int>> liParentCounts = new List<List<int>>();

            foreach (Step step in steps)
            {
                int count = steps.Count(c => c.SubSteps.Contains(step.StepId));
                liParentCounts.Add(new List<int>() { step.StepId, count });
            }

            List<Task> tasks = new List<Task>();
            CloudTable stepsTable = MicroflowTableHelper.GetStepsTable(projectRun.ProjectName);

            Step stepContainer = new Step(-1, "");
            steps.Insert(0, stepContainer);
            
            for (int i = 1; i < steps.Count; i++)
            {
                Step step = steps.ElementAt(i);

                int parentCount = (liParentCounts.FirstOrDefault(s => s.ElementAt(0) == step.StepId)
                                   ?? new List<int>() { step.StepId, 0 }).ElementAt(1);

                if (parentCount == 0)
                {
                    stepContainer.SubSteps.Add(step.StepId);
                }

                List<List<int>> subSteps = new List<List<int>>();

                foreach (int subId in step.SubSteps)
                {
                    int subParentCount = (liParentCounts.FirstOrDefault(s => s.ElementAt(0) == subId)
                                          ?? new List<int>() { step.StepId, 0 }).ElementAt(1);

                    subSteps.Add(new List<int>() { subId, subParentCount });
                }

                if (step.RetryOptions != null)
                {
                    HttpCallWithRetries httpCallRetriesEntity = new HttpCallWithRetries(projectRun.ProjectName, step.StepId, JsonSerializer.Serialize(subSteps))
                    {
                        CallBackAction = step.CallbackAction,
                        StopOnActionFailed = step.StopOnActionFailed,
                        Url = step.CalloutUrl,
                        ActionTimeoutSeconds = step.ActionTimeoutSeconds,
                        IsHttpGet = step.IsHttpGet
                    };

                    httpCallRetriesEntity.RetryDelaySeconds = step.RetryOptions.DelaySeconds;
                    httpCallRetriesEntity.RetryMaxDelaySeconds = step.RetryOptions.MaxDelaySeconds;
                    httpCallRetriesEntity.RetryMaxRetries = step.RetryOptions.MaxRetries;
                    httpCallRetriesEntity.RetryTimeoutSeconds = step.RetryOptions.TimeOutSeconds;
                    httpCallRetriesEntity.RetryBackoffCoefficient = step.RetryOptions.BackoffCoefficient;

                    // TODO: batchop this 
                    tasks.Add(httpCallRetriesEntity.InsertStep(stepsTable));
                }
                else
                {
                    HttpCall httpCallEntity = new HttpCall(projectRun.ProjectName, step.StepId, JsonSerializer.Serialize(subSteps))
                    {
                        CallBackAction = step.CallbackAction,
                        StopOnActionFailed = step.StopOnActionFailed,
                        Url = step.CalloutUrl,
                        ActionTimeoutSeconds = step.ActionTimeoutSeconds,
                        IsHttpGet = step.IsHttpGet
                    };

                    // TODO: batchop this 
                    tasks.Add(httpCallEntity.InsertStep(stepsTable));
                }
            }

            List<List<int>> containerSubSteps = new List<List<int>>();

            foreach (int sub in stepContainer.SubSteps)
            {
                containerSubSteps.Add(new List<int>() { sub, 1 });
            }

            HttpCall containerEntity = new HttpCall(projectRun.ProjectName, -1, JsonSerializer.Serialize(containerSubSteps));

            tasks.Add(containerEntity.InsertStep(stepsTable));

            await Task.WhenAll(tasks);
        }

        public static void ParseMergeFields(this string strWorkflow, ref Project project)
        {
            StringBuilder sb = new StringBuilder(strWorkflow);

            foreach (KeyValuePair<string, string> field in project.MergeFields)
            {
                sb.Replace("{" + field.Key + "}", field.Value);
            }

            project = JsonSerializer.Deserialize<Project>(sb.ToString());
        }
    }
}
