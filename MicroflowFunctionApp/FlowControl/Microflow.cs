using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microflow
{
    public static class Microflow
    {
        /// <summary>
        /// Recursive step execution and sub-step can execute now calculations
        /// </summary>
        /// <returns></returns>
        [FunctionName("ExecuteStep")]
        public static async Task ExecuteStep([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger inlog)
        {
            var log = context.CreateReplaySafeLogger(inlog);

            ProjectRun project = context.GetInput<ProjectRun>();

            // get project run payload and set the run object
            RunObject runObj = project.RunObject;

            HttpCallWithRetries httpCallWithRetries = null;

            try
            {
                // retries can be set for each step
                RetryOptions retryOptions = null;

                // get the step data from table storage (from PrepareWorkflow)
                Task<HttpCallWithRetries> httpCallWithRetriesTask = context.CallActivityAsync<HttpCallWithRetries>("GetStep", project);

                httpCallWithRetries = await httpCallWithRetriesTask;

                var logRowKey = httpCallWithRetries.RowKey.GetTableRowKeyDescendingByDate(context.CurrentUtcDateTime);

                // only do this for auto created container step
                if (project.RunObject.StepId == -1)
                {
                    List<KeyValuePair<int, int>> subSteps = JsonSerializer.Deserialize<List<KeyValuePair<int, int>>>(httpCallWithRetries.SubSteps);
                    var subTasks = new List<Task>();

                    foreach (var step in subSteps)
                    {
                        project.RunObject = new RunObject() { RunId = runObj.RunId, StepId = step.Key };
                        subTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", project));
                    }

                    await Task.WhenAll(subTasks);
                }
                // all substeps of the container step will follow the normal processing
                else
                {
                    if (httpCallWithRetries.Retry_DelaySeconds > 0)
                    {
                        retryOptions = MicroflowHelper.GetRetryOptions(httpCallWithRetries);
                    }

                    // TODO: project stop, pause and continue
                    //var stateTask = context.CallActivityAsync<int>("GetState", project.ProjectName);
                    //var state = await stateTask;
                    //if (state == 2)
                    //{
                    //    //var projectControlEnt = await context.CallActivityAsync<ProjectControlEntity>("GetProjectControl", project.ProjectName);

                    //    // wait for external event
                    //    await Task.Delay(30000);
                    //}

                    bool subOrchestratorSuccess = true;
                    var subTasks = new List<Task>();
                    var logTasks = new List<Task>();

                    // if the call out url is empty then no http call is done, use this for an empty conatainer step
                    if (!string.IsNullOrWhiteSpace(httpCallWithRetries.Url))
                    {
                        try
                        {
                            string id = context.NewGuid().ToString();

                            httpCallWithRetries.RunId = runObj.RunId;
                            httpCallWithRetries.MainOrchestrationId = project.OrchestratorInstanceId;

                            // log start of step
                            logTasks.Add(context.CallActivityAsync(
                                "LogStep",
                                new LogStepEntity(true, project.ProjectName, logRowKey, project.OrchestratorInstanceId)
                            ));

                            // wait for external event flow / callback
                            if (!string.IsNullOrWhiteSpace(httpCallWithRetries.CallBackAction))
                            {
                                var name = "HttpCallWithCallBackOrchestrator";

                                if (retryOptions == null)
                                {
                                    subOrchestratorSuccess = await context.CallSubOrchestratorAsync<bool>(name, id, httpCallWithRetries);
                                }
                                else
                                {
                                    subOrchestratorSuccess = await context.CallSubOrchestratorWithRetryAsync<bool>(name, retryOptions, id, httpCallWithRetries);
                                }
                            }
                            // send and receive inline flow
                            else
                            {
                                var name = "HttpCallOrchestrator";

                                if (retryOptions == null)
                                {
                                    subOrchestratorSuccess = await context.CallSubOrchestratorAsync<bool>(name, id, httpCallWithRetries);
                                }
                                else
                                {
                                    subOrchestratorSuccess = await context.CallSubOrchestratorWithRetryAsync<bool>(name, retryOptions, id, httpCallWithRetries);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.LogWarning($"Step {httpCallWithRetries.RowKey} an error result at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {project.RunObject.RunId}");

                            if (httpCallWithRetries.StopOnActionFailed)
                            {
                                throw;
                            }
                        }
                    }

                    // log end of step
                    logTasks.Add(context.CallActivityAsync(
                        "LogStep",
                        new LogStepEntity(false, project.ProjectName, logRowKey, project.OrchestratorInstanceId)
                    ));

                    if (subOrchestratorSuccess)
                    {
                        log.LogWarning($"Step {httpCallWithRetries.RowKey} done at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {project.RunObject.RunId}");

                        List<KeyValuePair<int, int>> subSteps = JsonSerializer.Deserialize<List<KeyValuePair<int, int>>>(httpCallWithRetries.SubSteps);

                        var canExeccuteTasks = new List<Task<CanExecuteResult>>();

                        foreach (var step in subSteps)
                        {
                            // step.Value is parentCount
                            // execute immediately if parentCount is 1
                            if (step.Value < 2)
                            {
                                // step.Key is stepId
                                project.RunObject = new RunObject() { RunId = runObj.RunId, StepId = step.Key };
                                subTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", project));
                            }
                            // if parentCount is more than 1, work out if it can execute now
                            else
                            {
                                canExeccuteTasks.Add(context.CallSubOrchestratorAsync<CanExecuteResult>("CanExecuteNow", new CanExecuteNowObject()
                                {
                                    RunId = runObj.RunId,
                                    StepId = step.Key,
                                    ParentCount = step.Value,
                                    ProjectName = project.ProjectName
                                }));

                                // the last parent to complete will trigger true so the sub step can execute
                                //if (canExecute)
                                //{
                                //    project.RunObject = new RunObject() { RunId = runObj.RunId, StepId = step.Key };
                                //    subStepTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", project));
                                //}
                            }
                        }

                        //await Task.WhenAll(canExeccuteTasks);
                        foreach (var task in canExeccuteTasks)
                        {
                            CanExecuteResult result = await task;

                            if (result.CanExecute)
                            {
                                project.RunObject = new RunObject()
                                {
                                    RunId = runObj.RunId,
                                    StepId = result.StepId
                                };

                                subTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", project));
                            }
                        }

                        await Task.WhenAll(logTasks);
                        await Task.WhenAll(subTasks);
                    }
                    else
                    {
                        log.LogError($"Step {httpCallWithRetries.RowKey} failed at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {project.RunObject.RunId}");
                    }
                }
            }
            catch (Exception e)
            {
                int? stepId = httpCallWithRetries == null ? -1 : Convert.ToInt32(httpCallWithRetries.RowKey);

                // log to table workflow completed
                var errorEntity = new LogErrorEntity(project.ProjectName, e.Message, runObj.RunId, stepId);
                await context.CallActivityAsync("LogError", errorEntity);
            }
        }
    }
}
