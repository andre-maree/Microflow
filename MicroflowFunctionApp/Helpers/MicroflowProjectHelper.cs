using System;
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

                // List<(string StepId, int ParentCount)> subSteps = JsonSerializer.Deserialize<List<(string StepId, int ParentCount)>>(httpCallWithRetries.SubSteps);

                List<Task> subTasks = new List<Task>();

                string[] stepsAndCounts = httpCallWithRetries.SubSteps.Split(new char[2] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries);

                for (int i1 = 0; i1 < stepsAndCounts.Length; i1 = i1 + 2)
                {
                    projectRun.RunObject = new RunObject() { RunId = guid, StepNumber = stepsAndCounts[i1] };

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
        public static async Task PrepareWorkflow(this ProjectRun projectRun, List<Step> steps, string StepIdFormat)
        {
            TableBatchOperation batch = new TableBatchOperation();
            List<Task> batchTasks = new List<Task>();
            CloudTable stepsTable = MicroflowTableHelper.GetStepsTable(projectRun.ProjectName);
            Step stepContainer = new Step(-1, null);
            StringBuilder sb = new StringBuilder();
            List<(int StepNumber, int ParentCount)> liParentCounts = new List<(int, int)>();

            foreach (Step step in steps)
            {
                int count = steps.Count(c => c.SubSteps.Contains(step.StepNumber));
                liParentCounts.Add((step.StepNumber, count));
            }

            for (int i = 0; i < steps.Count; i++)
            {
                Step step = steps.ElementAt(i);

                int parentCount = liParentCounts.FirstOrDefault(s => s.StepNumber == step.StepNumber).ParentCount;

                if (parentCount == 0)
                {
                    stepContainer.SubSteps.Add(step.StepNumber);
                }

                foreach (int subId in step.SubSteps)
                {
                    int subParentCount = liParentCounts.FirstOrDefault(s => s.StepNumber.Equals(subId)).ParentCount;

                    sb.Append(subId).Append(',').Append(subParentCount).Append(';');
                }

                if (step.RetryOptions != null)
                {
                    HttpCallWithRetries httpCallRetriesEntity = new HttpCallWithRetries(projectRun.ProjectName, step.StepNumber.ToString(), step.StepId, sb.ToString())
                    {
                        CallBackAction = step.CallbackAction,
                        StopOnActionFailed = step.StopOnActionFailed,
                        CalloutUrl = step.CalloutUrl,
                        ActionTimeoutSeconds = step.ActionTimeoutSeconds,
                        IsHttpGet = step.IsHttpGet
                    };

                    httpCallRetriesEntity.RetryDelaySeconds = step.RetryOptions.DelaySeconds;
                    httpCallRetriesEntity.RetryMaxDelaySeconds = step.RetryOptions.MaxDelaySeconds;
                    httpCallRetriesEntity.RetryMaxRetries = step.RetryOptions.MaxRetries;
                    httpCallRetriesEntity.RetryTimeoutSeconds = step.RetryOptions.TimeOutSeconds;
                    httpCallRetriesEntity.RetryBackoffCoefficient = step.RetryOptions.BackoffCoefficient;

                    // batchop
                    batch.Add(TableOperation.InsertOrReplace(httpCallRetriesEntity));
                }
                else
                {
                    HttpCall httpCallEntity = new HttpCall(projectRun.ProjectName, step.StepNumber.ToString(), step.StepId, sb.ToString())
                    {
                        CallBackAction = step.CallbackAction,
                        StopOnActionFailed = step.StopOnActionFailed,
                        CalloutUrl = step.CalloutUrl,
                        ActionTimeoutSeconds = step.ActionTimeoutSeconds,
                        IsHttpGet = step.IsHttpGet
                    };

                    // batchop
                    batch.Add(TableOperation.InsertOrReplace(httpCallEntity));
                }

                sb.Clear();

                if (batch.Count == 100)
                {
                    batchTasks.Add(stepsTable.InsertBatch(batch));
                    batch.Clear();
                }
            }

            foreach (int subId in stepContainer.SubSteps)
            {
                sb.Append(subId).Append(",1;");
            }

            HttpCall containerEntity = new HttpCall(projectRun.ProjectName, "-1", null, sb.ToString());

            batch.Add(TableOperation.InsertOrReplace(containerEntity));

            batchTasks.Add(stepsTable.InsertBatch(batch));

            await Task.WhenAll(batchTasks);
        }

        public static void ParseMergeFields(this string strWorkflow, ref MicroflowProject project)
        {
            StringBuilder sb = new StringBuilder(strWorkflow);

            foreach (KeyValuePair<string, string> field in project.MergeFields)
            {
                sb.Replace("{" + field.Key + "}", field.Value);
            }

            project = JsonSerializer.Deserialize<MicroflowProject>(sb.ToString());
        }
    }
}
