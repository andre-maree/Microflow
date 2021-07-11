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
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Microflow.Helpers
{
    public static class MicroflowProjectHelper
    {
        public static void SetProjectStateReady(this IDurableOrchestrationContext context, ProjectRun projectRun)
        {
            EntityId projStateId = new EntityId("ProjectState", projectRun.ProjectName);

            context.SignalEntity(projStateId, "ready");
        }

        /// <summary>
        /// Pause, run, or stop the project, cmd can be "run", "pause", or "stop"
        /// </summary>
        [FunctionName("Microflow_ProjectControl")]
        public static async Task<HttpResponseMessage> ProjectControl([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "ProjectControl/{cmd}/{projectName}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string projectName, string cmd)
        {
            EntityId runStateId = new EntityId(nameof(ProjectState), projectName);

            if (cmd.Equals("pause", StringComparison.OrdinalIgnoreCase))
            {
                await client.SignalEntityAsync(runStateId, "pause");
            }
            else if (cmd.Equals("run", StringComparison.OrdinalIgnoreCase))
            {
                await client.SignalEntityAsync(runStateId, "ready");
            }
            else if (cmd.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                await client.SignalEntityAsync(runStateId, "stop");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Pause, run, or stop all with the same global key, cmd can be "run", "pause", or "stop"
        /// </summary>
        [FunctionName("Microflow_GlobalControl")]
        public static async Task<HttpResponseMessage> GlobalControl([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "GlobalControl/{cmd}/{globalKey}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string globalKey, string cmd)
        {
            EntityId runStateId = new EntityId(nameof(GlobalState), globalKey);

            if (cmd.Equals("pause", StringComparison.OrdinalIgnoreCase))
            {
                await client.SignalEntityAsync(runStateId, "pause");
            }
            else if (cmd.Equals("run", StringComparison.OrdinalIgnoreCase))
            {
                await client.SignalEntityAsync(runStateId, "ready");
            }
            else if (cmd.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                await client.SignalEntityAsync(runStateId, "stop");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Durable entity check and set if the global state
        /// </summary>
        [FunctionName("GlobalState")]
        public static void GlobalState([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName)
            {
                case "ready":
                    ctx.SetState(0);
                    break;
                case "pause":
                    ctx.SetState(1);
                    break;
                case "stop":
                    ctx.SetState(2);
                    break;
                case "get":
                    ctx.Return(ctx.GetState<int>());
                    break;
            }
        }

        /// <summary>
        /// Durable entity check and set project state
        /// </summary>
        [FunctionName("ProjectState")]
        public static void ProjectState([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName)
            {
                case "ready":
                    ctx.SetState(0);
                    break;
                case "pause":
                    ctx.SetState(1);
                    break;
                case "stop":
                    ctx.SetState(2);
                    break;
                case "get":
                    ctx.Return(ctx.GetState<int>());
                    break;
            }
        }

        /// <summary>
        /// This must be called at least once before a project runs,
        /// this is to prevent multiple concurrent instances from writing step data at project run,
        /// call Microflow InsertOrUpdateProject when something changed in the workflow, but do not always call this when concurrent multiple workflows
        /// </summary>
        [FunctionName("Microflow_InsertOrUpdateProject")]
        public static async Task<HttpResponseMessage> SaveProject([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "InsertOrUpdateProject/{globalKey?}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string globalKey)
        {
            return await InserOrUpdateProject(req, client, globalKey);
        }

        /// <summary>
        /// Get global state
        /// </summary>
        [FunctionName("getGlobalState")]
        public static async Task<HttpResponseMessage> GetGlobalState([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "GlobalState/{globalKey}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string globalKey)
        {
            EntityId globalStateId = new EntityId(nameof(GlobalState), globalKey);
            Task<EntityStateResponse<int>> stateTask = client.ReadEntityStateAsync<int>(globalStateId);

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);

            await stateTask;

            resp.Content = new StringContent(stateTask.Result.EntityState.ToString());

            return resp;
        }

        /// <summary>
        /// Get project state
        /// </summary>
        [FunctionName("getProjectState")]
        public static async Task<HttpResponseMessage> GetProjectState([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "ProjectState/{projectName}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string projectName)
        {
            EntityId runStateId = new EntityId(nameof(ProjectState), projectName);
            Task<EntityStateResponse<int>> stateTask = client.ReadEntityStateAsync<int>(runStateId);

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);

            await stateTask;

            resp.Content = new StringContent(stateTask.Result.EntityState.ToString());

            return resp;
        }

        /// <summary>
        /// From the api call
        /// </summary>
        private static async Task<HttpResponseMessage> InserOrUpdateProject(HttpRequestMessage req, IDurableEntityClient client, string globalKey)
        {
            bool doneReadyFalse = false;

            // read http content
            string content = await req.Content.ReadAsStringAsync();

            // deserialize the workflow json
            MicroflowProject project = JsonSerializer.Deserialize<MicroflowProject>(content);

            //    // create a project run
            ProjectRun projectRun = new ProjectRun() { ProjectName = project.ProjectName, Loop = project.Loop };

            EntityId projStateId = new EntityId(nameof(ProjectState), projectRun.ProjectName);

            try
            {
                Task<EntityStateResponse<int>> globStateTask = null;

                if (!string.IsNullOrWhiteSpace(globalKey))
                {
                    EntityId globalStateId = new EntityId(nameof(GlobalState), globalKey);
                    globStateTask = client.ReadEntityStateAsync<int>(globalStateId);
                }
                // do not do anything, wait for the stopped project to be ready
                var projStateTask = client.ReadEntityStateAsync<int>(projStateId);
                int globState = 0;
                if (globStateTask != null)
                {
                    await globStateTask;
                    globState = globStateTask.Result.EntityState;
                }

                var projState = await projStateTask;
                if (projState.EntityState != 0 || globState != 0)
                {
                    return new HttpResponseMessage(HttpStatusCode.Locked);
                }

                // set project ready to false
                await client.SignalEntityAsync(projStateId, "pause");
                doneReadyFalse = true;

                // reate the storage tables for the project
                await MicroflowTableHelper.CreateTables(project.ProjectName);

                // upsert project control
                //Task projTask = MicroflowTableHelper.UpdateProjectControl(project.ProjectName, 0);

                //  clear step table data
                Task delTask = projectRun.DeleteSteps();

                //    // parse the mergefields
                content.ParseMergeFields(ref project);

                //await projTask;

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
                    await client.SignalEntityAsync(projStateId, "ready");
                }
            }
        }

        /// <summary>
        /// This is called from on start of workflow execution,
        /// does the looping and calls "ExecuteStep" for each top level step,
        /// by getting step -1 from table storage
        /// </summary>
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
        public static async Task<bool> CheckAndWaitForReadyToRun(this IDurableOrchestrationContext context, string projectName, ILogger log, string globalKey = null)
        {
            EntityId runState = new EntityId("ProjectState", projectName);
            Task<int> projStateTask = context.CallEntityAsync<int>(runState, "get");

            Task<EntityStateResponse<int>> globStateTask = null;

            if (!string.IsNullOrWhiteSpace(globalKey))
            {
                EntityId globalStateId = new EntityId("GlobalState", globalKey);
                globStateTask = context.CallEntityAsync<EntityStateResponse<int>>(globalStateId, "get");
            }

            int projState = await projStateTask;

            int globState = 0;
            if (globStateTask != null)
            {
                await globStateTask;
                globState = globStateTask.Result.EntityState;
            }

            if (projState != 0 || globState != 0)
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
