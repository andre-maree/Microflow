using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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
        [Deterministic]
        public static void SetProjectStateReady(this IDurableOrchestrationContext context, ProjectRun projectRun)
        {
            EntityId projStateId = new EntityId(MicroflowStateKeys.ProjectStateId, projectRun.ProjectName);

            context.SignalEntity(projStateId, MicroflowControlKeys.Ready);
        }

        /// <summary>
        /// From the api call
        /// </summary>
        public static async Task<HttpResponseMessage> InserOrUpdateProject(this IDurableEntityClient client, string content, string globalKey)
        {
            bool doneReadyFalse = false;

            // deserialize the workflow json
            MicroflowProject project = JsonSerializer.Deserialize<MicroflowProject>(content);

            //    // create a project run
            ProjectRun projectRun = new ProjectRun() { ProjectName = project.ProjectName, Loop = project.Loop };

            EntityId projStateId = new EntityId(MicroflowStateKeys.ProjectStateId, projectRun.ProjectName);

            try
            {
                Task<EntityStateResponse<int>> globStateTask = null;

                if (!string.IsNullOrWhiteSpace(globalKey))
                {
                    EntityId globalStateId = new EntityId(MicroflowStateKeys.GlobalStateId, globalKey);
                    globStateTask = client.ReadEntityStateAsync<int>(globalStateId);
                }
                // do not do anything, wait for the stopped project to be ready
                var projStateTask = client.ReadEntityStateAsync<int>(projStateId);
                int globState = MicroflowStates.Ready;
                if (globStateTask != null)
                {
                    await globStateTask;
                    globState = globStateTask.Result.EntityState;
                }

                var projState = await projStateTask;
                if (projState.EntityState != MicroflowStates.Ready || globState != MicroflowStates.Ready)
                {
                    return new HttpResponseMessage(HttpStatusCode.Locked);
                }

                // set project ready to false
                await client.SignalEntityAsync(projStateId, MicroflowControlKeys.Pause);
                doneReadyFalse = true;

                // reate the storage tables for the project
                await MicroflowTableHelper.CreateTables(project.ProjectName);

                //  clear step table data
                Task delTask = projectRun.DeleteSteps();

                //    // parse the mergefields
                content.ParseMergeFields(ref project);

                await delTask;

                // prepare the workflow by persisting parent info to table storage
                await projectRun.PrepareWorkflow(project.Steps, project.StepIdFormat);

                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }
            catch (StorageException e)
            {
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(e.Message)
                };

                return resp;
            }
            catch (Exception e)
            {
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(e.Message)
                };

                try
                {
                    await MicroflowHelper.LogError(project.ProjectName ?? "no project", projectRun.RunObject.GlobalKey, projectRun.RunObject.RunId, e);
                }
                catch
                {
                    resp.StatusCode = HttpStatusCode.InternalServerError;
                }

                return resp;
            }
            finally
            {
                // if project ready was set to false, always set it to true
                if (doneReadyFalse)
                {
                    await client.SignalEntityAsync(projStateId, MicroflowControlKeys.Ready);
                }
            }
        }

        /// <summary>
        /// This is called from on start of workflow execution,
        /// does the looping and calls "ExecuteStep" for each top level step,
        /// by getting step -1 from table storage
        /// </summary>
        [Deterministic]
        public static async Task MicroflowStartProjectRun(this IDurableOrchestrationContext context, ILogger log, ProjectRun projectRun)
        {
            // do the looping
            for (int i = 1; i <= projectRun.Loop; i++)
            {
                // get the top container step from table storage (from PrepareWorkflow)
                Task<HttpCallWithRetries> httpTask = context.CallActivityAsync<HttpCallWithRetries>("GetStep", projectRun);

                string guid = context.NewGuid().ToString();

                projectRun.RunObject.RunId = guid;

                log.LogError($"Started Run ID {projectRun.RunObject.RunId}...");

                // pass in the current loop count so it can be used downstream/passed to the micro-services
                projectRun.CurrentLoop = i;

                List<Task> subTasks = new List<Task>();

                HttpCallWithRetries httpCallWithRetries = await httpTask;

                string[] stepsAndCounts = httpCallWithRetries.SubSteps.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                for (int j = 0; j < stepsAndCounts.Length; j += 2)
                {
                    projectRun.RunObject = new RunObject()
                    {
                        RunId = guid,
                        StepNumber = stepsAndCounts[j],
                        GlobalKey = projectRun.RunObject.GlobalKey
                    };

                    subTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", projectRun));
                }

                await Task.WhenAll(subTasks);

                log.LogError($"Run ID {guid} completed successfully...");
            }
        }

        /// <summary>
        /// Check if project ready is true, else wait with a timer (this is a durable monitor), called from start
        /// </summary>
        [Deterministic]
        public static async Task<bool> CheckAndWaitForReadyToRun(this IDurableOrchestrationContext context, string projectName, ILogger log, string globalKey = null)
        {
            EntityId runStateId = new EntityId(MicroflowStateKeys.ProjectStateId, projectName);

            Task<int> projStateTask = context.CallEntityAsync<int>(runStateId, MicroflowControlKeys.Read);
            Task<EntityStateResponse<int>> globStateTask = null;

            if (!string.IsNullOrWhiteSpace(globalKey))
            {
                EntityId globalStateId = new EntityId(MicroflowStateKeys.GlobalStateId, globalKey);
                globStateTask = context.CallEntityAsync<EntityStateResponse<int>>(globalStateId, MicroflowControlKeys.Read);
            }

            int projState = await projStateTask;
            int globState = MicroflowStates.Ready;

            if (globStateTask != null)
            {
                EntityStateResponse<int> globStateRes = await globStateTask;
                globState = globStateRes.EntityState;
            }

            if (projState != MicroflowStates.Ready || globState != MicroflowStates.Ready)
            {
                return false;
            }

            return true;

            //DateTime endDate = context.CurrentUtcDateTime.AddMinutes(30);
            //int count = 5;
            //int max = 20;

            //using (CancellationTokenSource cts = new CancellationTokenSource())
            //{
            //    try
            //    {
            //        while (context.CurrentUtcDateTime < endDate)
            //        {
            //            DateTime deadline = context.CurrentUtcDateTime.Add(TimeSpan.FromSeconds(count < max ? count : max));
            //            await context.CreateTimer(deadline, cts.Token);
            //            count++;

            //            if (await context.CallEntityAsync<int>(runState, "get") == 0)
            //            {
            //                break;
            //            }
            //        }
            //    }
            //    catch (TaskCanceledException)
            //    {
            //        log.LogCritical("========================TaskCanceledException==========================");
            //    }
            //    finally
            //    {
            //        cts.Dispose();
            //    }
            //}
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
            CloudTable stepsTable = MicroflowTableHelper.GetStepsTable();
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
                        CalloutTimeoutSeconds = step.CalloutTimeoutSeconds,
                        IsHttpGet = step.IsHttpGet,
                        AsynchronousPollingEnabled = step.AsynchronousPollingEnabled
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
                        CalloutTimeoutSeconds = step.CalloutTimeoutSeconds,
                        IsHttpGet = step.IsHttpGet,
                        AsynchronousPollingEnabled = step.AsynchronousPollingEnabled
                    };

                    // batchop
                    batch.Add(TableOperation.InsertOrReplace(httpCallEntity));
                }

                sb.Clear();

                if (batch.Count == 100)
                {
                    batchTasks.Add(stepsTable.ExecuteBatchAsync(batch));
                    batch = new TableBatchOperation();
                }
            }

            foreach (int subId in stepContainer.SubSteps)
            {
                sb.Append(subId).Append(",1;");
            }

            HttpCall containerEntity = new HttpCall(projectRun.ProjectName, "-1", null, sb.ToString());

            batch.Add(TableOperation.InsertOrReplace(containerEntity));

            batchTasks.Add(stepsTable.ExecuteBatchAsync(batch));

            await Task.WhenAll(batchTasks);
        }

        /// <summary>
        /// Parse all the merge fields in the project
        /// </summary>
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
