using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MicroflowModels;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.Helpers
{
    public static class MicroflowHelper
    {
        public static RetryOptions GetTableLoggingRetryOptions()
        {
            return new RetryOptions(TimeSpan.FromSeconds(15), 10)
            {
                MaxRetryInterval = TimeSpan.FromSeconds(1000),
                BackoffCoefficient = 1.5,
                RetryTimeout = TimeSpan.FromSeconds(30)
            };
        }

        public static RetryOptions GetRetryOptions(HttpCallWithRetries httpCallWithRetries)
        {
            RetryOptions ops = new RetryOptions(TimeSpan.FromSeconds(httpCallWithRetries.Retry_DelaySeconds), httpCallWithRetries.Retry_MaxRetries);
            ops.RetryTimeout = TimeSpan.FromSeconds(httpCallWithRetries.Retry_TimeoutSeconds);
            ops.MaxRetryInterval = TimeSpan.FromSeconds(httpCallWithRetries.Retry_MaxDelaySeconds);
            ops.BackoffCoefficient = httpCallWithRetries.Retry_BackoffCoefficient;

            return ops;
        }

        /// <summary>
        /// Called before a workflow executes and takes the top step and recursives it to insert step configs into table storage
        /// </summary>
        public static async Task<List<Step>> PrepareWorkflow(string instanceId, ProjectRun projectRun, List<Step> steps, Dictionary<string, string> mergeFields)
        {
            HashSet<KeyValuePair<int, int>> hsStepCounts = new HashSet<KeyValuePair<int, int>>();

            Local(steps[0]);

            void Local(Step step)
            {
                if (step.SubSteps != null)
                {
                    foreach (var cstep in step.SubSteps)
                    {
                        hsStepCounts.Add(new KeyValuePair<int, int>(step.StepId, cstep));
                        Local(steps[cstep - 1]);
                    }
                }
                else
                {
                    step.SubSteps = new List<int>();
                }
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
                    var parents = steps.Where(x => x.SubSteps.Contains(step.StepId)).ToList();
                    if (parents.Count == 0)
                    {
                        stepContainer.SubSteps.Add(step.StepId);
                    }

                    step.CalloutUrl = step.CalloutUrl.Replace("<instanceId>", projectRun.RunObject.RunId, StringComparison.OrdinalIgnoreCase);
                    step.CalloutUrl = step.CalloutUrl.Replace("<runId>", projectRun.RunObject.RunId, StringComparison.OrdinalIgnoreCase);
                    step.CalloutUrl = step.CalloutUrl.Replace("<stepId>", step.StepId.ToString(), StringComparison.OrdinalIgnoreCase);

                    List<KeyValuePair<int, int>> substeps = new List<KeyValuePair<int, int>>();

                    foreach (var sub in step.SubSteps)
                    {
                        var count = hsStepCounts.Count(x => x.Value == sub);
                        substeps.Add(new KeyValuePair<int, int>(sub, count));
                    }

                    if (step.RetryOptions != null)
                    {
                        HttpCallWithRetries stentRetries = new HttpCallWithRetries(projectRun.ProjectName, step.StepId, JsonSerializer.Serialize(substeps))
                        {
                            CallBackAction = step.CallbackAction,
                            StopOnActionFailed = step.StopOnActionFailed,
                            Url = step.CalloutUrl,
                            ActionTimeoutSeconds = step.ActionTimeoutSeconds
                        };

                        stentRetries.Retry_DelaySeconds = step.RetryOptions.DelaySeconds;
                        stentRetries.Retry_MaxDelaySeconds = step.RetryOptions.MaxDelaySeconds;
                        stentRetries.Retry_MaxRetries = step.RetryOptions.MaxRetries;
                        stentRetries.Retry_TimeoutSeconds = step.RetryOptions.TimeOutSeconds;
                        stentRetries.Retry_BackoffCoefficient = step.RetryOptions.BackoffCoefficient;

                        tasks.Add(MicroflowTableHelper.InsertStep(stentRetries, stepsTable));
                    }
                    else
                    {
                        HttpCall stent = new HttpCall(projectRun.ProjectName, step.StepId, JsonSerializer.Serialize(substeps))
                        {
                            CallBackAction = step.CallbackAction,
                            StopOnActionFailed = step.StopOnActionFailed,
                            Url = step.CalloutUrl,
                            ActionTimeoutSeconds = step.ActionTimeoutSeconds
                        };

                        tasks.Add(MicroflowTableHelper.InsertStep(stent, stepsTable));
                    }
                }
            }

            List<KeyValuePair<int, int>> containersubsteps = new List<KeyValuePair<int, int>>();
            foreach (var substep in stepContainer.SubSteps)
            {
                containersubsteps.Add(new KeyValuePair<int, int>(substep, 1));
            }

            HttpCall containerEntity = new HttpCall(projectRun.ProjectName, -1, JsonSerializer.Serialize(containersubsteps));
            tasks.Add(MicroflowTableHelper.InsertStep(containerEntity, stepsTable));
            await Task.WhenAll(tasks);

            return steps;
        }

        private const string CharList = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";

        public static string CreateSubOrchestrationId()
        {
            Random r = new Random();
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < 6; i++)
            {
                sb.Append(CharList[r.Next(0, 62)]);
            }
            return sb.ToString();
        }

        public static void ParseMergeFields(string strWorkflow, ref Project project)
        {
            StringBuilder sb = new StringBuilder(strWorkflow);

            foreach (var field in project.MergeFields)
            {
                sb.Replace("{" + field.Key + "}", field.Value);
            }

            sb.Replace("{workflowId}", "");
            sb.Replace("{stepId}", "");

            project = JsonSerializer.Deserialize<Project>(sb.ToString());
        }

        public static DurableHttpRequest CreateMicroflowDurableHttpRequest(HttpCall httpCall, string instanceId)
        {
            //httpCall.PartitionKey = instanceId;
            MicroflowPostData postData = new MicroflowPostData()
            {
                ProjectName = httpCall.PartitionKey,
                SubOrchestrationId = instanceId,
                RunId = httpCall.RunId,
                StepId = httpCall.RowKey,
                MainOrchestrationId = httpCall.MainOrchestrationId,
                CallbackUrl = string.IsNullOrWhiteSpace(httpCall.CallBackAction) 
                ? "" 
                : $"{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}/api/{httpCall.CallBackAction}/{instanceId}/{httpCall.RowKey}"
            };

            string body = JsonSerializer.Serialize(postData);

            DurableHttpRequest newDurableHttpRequest = new DurableHttpRequest(
                method: HttpMethod.Post,
                uri: new Uri(httpCall.Url),
                timeout: TimeSpan.FromSeconds(httpCall.ActionTimeoutSeconds),
            //headers: durableHttpRequest.Headers,
            content: body // This is the line causing the issue
                          //tokenSource: durableHttpRequest.TokenSource

            );

            // Do not copy over the x-functions-key header, as in many cases, the
            // functions key used for the initial request will be a Function-level key
            // and the status endpoint requires a master key.
            newDurableHttpRequest.Headers.Remove("x-functions-key");

            return newDurableHttpRequest;
        }

        public class StepComparer : IEqualityComparer<Step>
        {
            public bool Equals(Step x, Step y)
            {
                return y != null && x != null && x.StepId == y.StepId;
            }

            public int GetHashCode(Step obj)
            {
                return obj.StepId.GetHashCode();
            }
        }
    }
}
