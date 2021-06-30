using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microflow.Models;
using MicroflowModels;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microflow.Helpers
{
    public static class MicroflowProjectHelper
    {
        public static async Task MicroflowStartProjectRun(this IDurableOrchestrationContext context, ILogger log, ProjectRun projectRun)
        {
            // do the looping
            for (int i = 1; i <= projectRun.Loop; i++)
            {
                // get the top container step from table storage (from PrepareWorkflow)
                HttpCallWithRetries httpCallWithRetries = await context.CallActivityAsync<HttpCallWithRetries>("GetStep", projectRun);

                var guid = context.NewGuid().ToString();

                projectRun.RunObject.RunId = guid;

                log.LogError($"Started Run ID {projectRun.RunObject.RunId}...");

                // pass in the current loop count so it can be used downstream/passed to the micro-services
                projectRun.CurrentLoop = i;

                List<KeyValuePair<int, int>> subSteps = JsonSerializer.Deserialize<List<KeyValuePair<int, int>>>(httpCallWithRetries.SubSteps);
                var subTasks = new List<Task>();

                foreach (var step in subSteps)
                {
                    projectRun.RunObject = new RunObject() { RunId = guid, StepId = step.Key };

                    subTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", projectRun));
                }

                await Task.WhenAll(subTasks);

                log.LogError($"Run ID {guid} completed successfully...");
            }
        }

        /// <summary>
        /// Called before a workflow executes and takes the top step and recursives it to insert step configs into table storage
        /// </summary>
        public static async Task PrepareWorkflow(this ProjectRun projectRun, List<Step> steps)
        {
            HashSet<KeyValuePair<int, int>> hsStepCounts = new HashSet<KeyValuePair<int, int>>();

            foreach(Step step in steps)
            {
                int count = steps.Count(c => c.SubSteps.Contains(step.StepId));
                hsStepCounts.Add(new KeyValuePair<int, int>(step.StepId, count));
            }

            var tasks = new List<Task>();
            var stepsTable = MicroflowTableHelper.GetStepsTable(projectRun.ProjectName);

            Step stepContainer = new Step(-1, "");
            steps.Insert(0, stepContainer);

            for (int i = 0; i < steps.Count; i++)
            {
                Step step = steps.ElementAt(i);

                if (step.StepId > -1)
                {
                    int parentCount = hsStepCounts.FirstOrDefault(s => s.Key == step.StepId).Value;
                    if (parentCount == 0)
                    {
                        stepContainer.SubSteps.Add(step.StepId);
                    }

                    List<KeyValuePair<int, int>> substeps = new List<KeyValuePair<int, int>>();

                    foreach (var sub in step.SubSteps)
                    {
                        parentCount = hsStepCounts.FirstOrDefault(s => s.Key == sub).Value;
                        substeps.Add(new KeyValuePair<int, int>(sub, parentCount));
                    }

                    if (step.RetryOptions != null)
                    {
                        HttpCallWithRetries stentRetries = new HttpCallWithRetries(projectRun.ProjectName, step.StepId, JsonSerializer.Serialize(substeps))
                        {
                            CallBackAction = step.CallbackAction,
                            StopOnActionFailed = step.StopOnActionFailed,
                            Url = step.CalloutUrl,
                            ActionTimeoutSeconds = step.ActionTimeoutSeconds,
                            IsHttpGet = step.IsHttpGet
                        };

                        stentRetries.RetryDelaySeconds = step.RetryOptions.DelaySeconds;
                        stentRetries.RetryMaxDelaySeconds = step.RetryOptions.MaxDelaySeconds;
                        stentRetries.RetryMaxRetries = step.RetryOptions.MaxRetries;
                        stentRetries.RetryTimeoutSeconds = step.RetryOptions.TimeOutSeconds;
                        stentRetries.RetryBackoffCoefficient = step.RetryOptions.BackoffCoefficient;

                        // TODO: batchop this 
                        tasks.Add(stentRetries.InsertStep(stepsTable));
                    }
                    else
                    {
                        HttpCall stent = new HttpCall(projectRun.ProjectName, step.StepId, JsonSerializer.Serialize(substeps))
                        {
                            CallBackAction = step.CallbackAction,
                            StopOnActionFailed = step.StopOnActionFailed,
                            Url = step.CalloutUrl,
                            ActionTimeoutSeconds = step.ActionTimeoutSeconds,
                            IsHttpGet = step.IsHttpGet
                        };
                        
                        // TODO: batchop this 
                        tasks.Add(stent.InsertStep(stepsTable));
                    }
                }
            }

            List<KeyValuePair<int, int>> containersubsteps = new List<KeyValuePair<int, int>>();
            foreach (var substep in stepContainer.SubSteps)
            {
                containersubsteps.Add(new KeyValuePair<int, int>(substep, 1));
            }

            HttpCall containerEntity = new HttpCall(projectRun.ProjectName, -1, JsonSerializer.Serialize(containersubsteps));
            tasks.Add(containerEntity.InsertStep(stepsTable));

            await Task.WhenAll(tasks);
        }

        public static void ParseMergeFields(this string strWorkflow, ref Project project)
        {
            StringBuilder sb = new StringBuilder(strWorkflow);

            foreach (var field in project.MergeFields)
            {
                sb.Replace("{" + field.Key + "}", field.Value);
            }

            project = JsonSerializer.Deserialize<Project>(sb.ToString());
        }

        public static string ParseUrlMicroflowData(this HttpCall httpCall, string instanceId, string callbackUrl)
        {
            StringBuilder sb = new StringBuilder(httpCall.Url);

            sb.Replace("<ProjectName>", httpCall.PartitionKey);
            sb.Replace("<MainOrchestrationId>", httpCall.MainOrchestrationId);
            sb.Replace("<SubOrchestrationId>", instanceId);
            sb.Replace("<CallbackUrl>", callbackUrl);
            sb.Replace("<RunId>", httpCall.RunId);
            sb.Replace("<StepId>", httpCall.RowKey);

            return sb.ToString();
        }
    }
}
