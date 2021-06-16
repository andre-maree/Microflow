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

            // get project run payload and set the run object
            ProjectRun project = context.GetInput<ProjectRun>();
            RunObject runObj = project.RunObject;
                     
            // retries can be set for each step
            RetryOptions retryOptions = null;

            // get the step data from table storage (from PrepareWorkflow)
            Task<HttpCallWithRetries> httpCallWithRetriesTask = context.CallActivityAsync<HttpCallWithRetries>("GetStep", project);

            HttpCallWithRetries httpCallWithRetries = await httpCallWithRetriesTask;

            // if a delay is set, get the retry option for the step
            if (httpCallWithRetries.Retry_DelaySeconds > 0)
            {
                retryOptions = MicroflowHelper.GetRetryOptions(httpCallWithRetries);
            }

            // TODO: project stop, pause and continue
            //var stateTask = context.CallActivityAsync<int>("GetState", project.ProjectId);
            //var state = await stateTask;
            //if (state == 2)
            //{
            //    //var projectControlEnt = await context.CallActivityAsync<ProjectControlEntity>("GetProjectControl", project.ProjectId);

            //    // wait for external event
            //    await Task.Delay(30000);
            //}

            bool subOrchestratorSuccess = true;

            // if the call out url is empty then no http call is done, use this for an empty conatainer step
            if(!string.IsNullOrWhiteSpace(httpCallWithRetries.Url))
            {
                try
                {
                    // wait for external event flow / callback
                    if (!string.IsNullOrWhiteSpace(httpCallWithRetries.CallBackAction))
                    {
                        if (retryOptions == null)
                            subOrchestratorSuccess = await context.CallSubOrchestratorAsync<bool>("HttpCallWithCallBackOrchestrator", MicroflowHelper.CreateSubOrchestrationId(), httpCallWithRetries);
                        else
                            subOrchestratorSuccess = await context.CallSubOrchestratorWithRetryAsync<bool>("HttpCallWithCallBackOrchestrator", retryOptions, MicroflowHelper.CreateSubOrchestrationId(), httpCallWithRetries);
                    }
                    // send and receive inline flow
                    else
                    {
                        if (retryOptions == null)
                            subOrchestratorSuccess = await context.CallSubOrchestratorAsync<bool>("HttpCallOrchestrator", MicroflowHelper.CreateSubOrchestrationId(), httpCallWithRetries);
                        else
                            subOrchestratorSuccess = await context.CallSubOrchestratorWithRetryAsync<bool>("HttpCallOrchestrator", retryOptions, MicroflowHelper.CreateSubOrchestrationId(), httpCallWithRetries);
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
            
            if (subOrchestratorSuccess)
            {
                // log to table workflow completed
                RetryOptions retryOptionsLog = new RetryOptions(TimeSpan.FromSeconds(15), 10)
                {
                    MaxRetryInterval = TimeSpan.FromSeconds(1000),
                    BackoffCoefficient = 1.5,
                    RetryTimeout = TimeSpan.FromSeconds(30)
                };

                await context.CallSubOrchestratorWithRetryAsync("TableLogStep", retryOptionsLog, new LogStepEntity(httpCallWithRetries.RowKey, runObj.RunId, $"Step in project {project.ProjectId} completed successfully"));


                log.LogWarning($"Step {httpCallWithRetries.RowKey} done at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {project.RunObject.RunId}");

                var subStepTasks = new List<Task>();

                List<KeyValuePair<int, int>> subSteps = JsonSerializer.Deserialize<List<KeyValuePair<int, int>>>(httpCallWithRetries.SubSteps);

                foreach (var step in subSteps)
                {
                    // step.Value is parentCount
                    // execute immediately if parentCount is 1
                    if (step.Value < 2)
                    {
                        // step.Key is stepId
                        project.RunObject = new RunObject() { RunId = runObj.RunId, StepId = step.Key };
                        subStepTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", project));
                    }
                    // if parentCount is more than 1, work out if it can execute now
                    else
                    {
                        bool canExecute = await context.CallSubOrchestratorAsync<bool>("CanExecuteNow", new CanExecuteNowObject() { RunId = runObj.RunId, StepId = step.Key, ParentCount = step.Value, ProjectId = project.ProjectId });

                        // the last parent to complete will trigger true so the sub step can execute
                        if (canExecute)
                        {
                            project.RunObject = new RunObject() { RunId = runObj.RunId, StepId = step.Key };
                            subStepTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", project));
                        }
                    }
                }

                await Task.WhenAll(subStepTasks);
            }
            else
            {
                log.LogError($"Step {httpCallWithRetries.RowKey} failed at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {project.RunObject.RunId}");
            }
        }
    }
}