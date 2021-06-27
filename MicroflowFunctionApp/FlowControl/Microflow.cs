using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microflow.FlowControl
{
    public static class Microflow
    {
        /// <summary>
        /// Recursive step execution and sub-step can execute now calculations
        /// </summary>
        [FunctionName("ExecuteStep")]
        public static async Task ExecuteStep([OrchestrationTrigger] IDurableOrchestrationContext context,
                                             ILogger inlog)
        {
            var log = context.CreateReplaySafeLogger(inlog);

            ProjectRun projectRun = context.GetInput<ProjectRun>();

            // get project run payload and set the run object
            RunObject runObj = projectRun.RunObject;

            HttpCallWithRetries httpCallWithRetries = null;

            try
            {
                // retries can be set for each step
                RetryOptions retryOptions = null;

                // get the step data from table storage (from PrepareWorkflow)
                Task<HttpCallWithRetries> httpCallWithRetriesTask = context.CallActivityAsync<HttpCallWithRetries>("GetStep", projectRun);

                httpCallWithRetries = await httpCallWithRetriesTask;

                var logRowKey = MicroflowTableHelper.GetTableLogRowKeyDescendingByDate(context.CurrentUtcDateTime, context.NewGuid().ToString());

                if (httpCallWithRetries.Retry_DelaySeconds > 0)
                {
                    retryOptions = httpCallWithRetries.GetRetryOptions();
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

                MicroflowHttpResponse microflowHttpResponse = null;
                var subTasks = new List<Task>();
                var logTasks = new List<Task>();

                // if the call out url is empty then no http call is done, use this for an empty conatainer step
                if (!string.IsNullOrWhiteSpace(httpCallWithRetries.Url))
                {
                    microflowHttpResponse = await StepCallout(context, log, projectRun, runObj, httpCallWithRetries, retryOptions, logRowKey, microflowHttpResponse, logTasks);
                }

                if (microflowHttpResponse.Success || !httpCallWithRetries.StopOnActionFailed)
                {
                    LogStepEnd(context, log, projectRun, httpCallWithRetries, logRowKey, microflowHttpResponse, logTasks);

                    List<KeyValuePair<int, int>> subSteps = JsonSerializer.Deserialize<List<KeyValuePair<int, int>>>(httpCallWithRetries.SubSteps);

                    var canExeccuteTasks = new List<Task<CanExecuteResult>>();

                    foreach (var step in subSteps)
                    {
                        // step.Value is parentCount
                        // execute immediately if parentCount is 1
                        if (step.Value < 2)
                        {
                            // step.Key is stepId
                            projectRun.RunObject = new RunObject() { RunId = runObj.RunId, StepId = step.Key };
                            subTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", projectRun));
                        }
                        // if parentCount is more than 1, work out if it can execute now
                        else
                        {
                            canExeccuteTasks.Add(context.CallSubOrchestratorAsync<CanExecuteResult>("CanExecuteNow", new CanExecuteNowObject()
                            {
                                RunId = runObj.RunId,
                                StepId = step.Key,
                                ParentCount = step.Value,
                                ProjectName = projectRun.ProjectName
                            }));
                        }
                    }

                    foreach (var task in canExeccuteTasks)
                    {
                        CanExecuteResult result = await task;

                        if (result.CanExecute)
                        {
                            projectRun.RunObject = new RunObject()
                            {
                                RunId = runObj.RunId,
                                StepId = result.StepId
                            };

                            subTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", projectRun));
                        }
                    }

                    await Task.WhenAll(subTasks);
                }
                else
                {
                    LogStepFail(context, log, projectRun, httpCallWithRetries, logRowKey, microflowHttpResponse, logTasks);
                }

                await Task.WhenAll(logTasks);
            }
            catch (Exception e)
            {
                int? stepId = httpCallWithRetries == null ? -1 : Convert.ToInt32(httpCallWithRetries.RowKey);

                // log to table workflow completed
                var errorEntity = new LogErrorEntity(projectRun.ProjectName, e.Message, runObj.RunId, stepId);
                await context.CallActivityAsync("LogError", errorEntity);
            }
        }

        /// <summary>
        /// Do the step callout
        /// </summary>
        private static async Task<MicroflowHttpResponse> StepCallout(IDurableOrchestrationContext context,
                                                                   ILogger log,
                                                                   ProjectRun projectRun,
                                                                   RunObject runObj,
                                                                   HttpCallWithRetries httpCallWithRetries,
                                                                   RetryOptions retryOptions,
                                                                   string logRowKey,
                                                                   MicroflowHttpResponse microflowHttpResponse,
                                                                   List<Task> logTasks)
        {
            try
            {
                string id = context.NewGuid().ToString();

                httpCallWithRetries.RunId = runObj.RunId;
                httpCallWithRetries.MainOrchestrationId = projectRun.OrchestratorInstanceId;

                // log start of step
                LogStepStart(context, projectRun, httpCallWithRetries, logRowKey, logTasks);

                // wait for external event flow / callback
                if (!string.IsNullOrWhiteSpace(httpCallWithRetries.CallBackAction))
                {
                    var name = "HttpCallWithCallBackOrchestrator";

                    if (retryOptions == null)
                    {
                        microflowHttpResponse = await context.CallSubOrchestratorAsync<MicroflowHttpResponse>(name, id, httpCallWithRetries);
                    }
                    else
                    {
                        microflowHttpResponse = await context.CallSubOrchestratorWithRetryAsync<MicroflowHttpResponse>(name, retryOptions, id, httpCallWithRetries);
                    }
                }
                // send and receive inline flow
                else
                {
                    var name = "HttpCallOrchestrator";

                    if (retryOptions == null)
                    {
                        microflowHttpResponse = await context.CallSubOrchestratorAsync<MicroflowHttpResponse>(name, id, httpCallWithRetries);
                    }
                    else
                    {
                        microflowHttpResponse = await context.CallSubOrchestratorWithRetryAsync<MicroflowHttpResponse>(name, retryOptions, id, httpCallWithRetries);
                    }
                }
            }
            catch (Exception ex)
            {
                microflowHttpResponse = await HandleCalloutException(context, log, projectRun, httpCallWithRetries, logRowKey, logTasks, ex);
            }

            return microflowHttpResponse;
        }

        /// <summary>
        /// Log the fail of step
        /// </summary>
        private static void LogStepFail(IDurableOrchestrationContext context,
                                        ILogger log,
                                        ProjectRun projectRun,
                                        HttpCallWithRetries httpCallWithRetries,
                                        string logRowKey,
                                        MicroflowHttpResponse microflowHttpResponse,
                                        List<Task> logTasks)
        {
            log.LogError($"Step {httpCallWithRetries.RowKey} failed at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {projectRun.RunObject.RunId}");

            // log step error, stop exe
            logTasks.Add(context.CallActivityAsync(
                "LogStep",
                new LogStepEntity(false,
                                  projectRun.ProjectName,
                                  logRowKey,
                                  httpCallWithRetries.RowKey,
                                  projectRun.OrchestratorInstanceId,
                                  false,
                                  microflowHttpResponse.HttpResponseStatusCode,
                                  string.IsNullOrWhiteSpace(microflowHttpResponse.Message) ? null : microflowHttpResponse.Message)
            ));
        }

        /// <summary>
        /// Log the start of step
        /// </summary>
        private static void LogStepStart(IDurableOrchestrationContext context,
                                         ProjectRun projectRun,
                                         HttpCallWithRetries httpCallWithRetries,
                                         string logRowKey,
                                         List<Task> logTasks)
        {
            // log start of step
            logTasks.Add(context.CallActivityAsync(
                "LogStep",
                new LogStepEntity(true, projectRun.ProjectName, logRowKey, httpCallWithRetries.RowKey, projectRun.OrchestratorInstanceId)
            ));
        }

        /// <summary>
        /// Log the end of step
        /// </summary>
        private static void LogStepEnd(IDurableOrchestrationContext context,
                                       ILogger log,
                                       ProjectRun projectRun,
                                       HttpCallWithRetries httpCallWithRetries,
                                       string logRowKey,
                                       MicroflowHttpResponse microflowHttpResponse,
                                       List<Task> logTasks)
        {
            // log end of step
            logTasks.Add(context.CallActivityAsync(
                "LogStep",
                new LogStepEntity(false,
                                  projectRun.ProjectName,
                                  logRowKey,
                                  httpCallWithRetries.RowKey,
                                  projectRun.OrchestratorInstanceId,
                                  microflowHttpResponse.Success,
                                  microflowHttpResponse.HttpResponseStatusCode,
                                  string.IsNullOrWhiteSpace(microflowHttpResponse.Message) ? null : microflowHttpResponse.Message)
            ));

            log.LogWarning($"Step {httpCallWithRetries.RowKey} done at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {projectRun.RunObject.RunId}");
        }

        /// <summary>
        /// Handle the step execution exception
        /// </summary>
        private static async Task<MicroflowHttpResponse> HandleCalloutException(IDurableOrchestrationContext context,
                                                                                ILogger log,
                                                                                ProjectRun projectRun,
                                                                                HttpCallWithRetries httpCallWithRetries,
                                                                                string logRowKey,
                                                                                List<Task> logTasks,
                                                                                Exception ex)
        {
            log.LogWarning($"Step {httpCallWithRetries.RowKey} an error result at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {projectRun.RunObject.RunId}");

            if (!httpCallWithRetries.StopOnActionFailed)
            {
                if (ex.InnerException is TimeoutException)
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = false,
                        HttpResponseStatusCode = 408,
                        Message = "action timeout"
                    };
                }
                else
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = false,
                        HttpResponseStatusCode = 500,
                        Message = ex.Message
                    };
                }
            }
            else
            {
                if (ex.InnerException is TimeoutException)
                {
                    // log step error, stop exe
                    logTasks.Add(context.CallActivityAsync(
                        "LogStep",
                        new LogStepEntity(false,
                                          projectRun.ProjectName,
                                          logRowKey,
                                          httpCallWithRetries.RowKey,
                                          projectRun.OrchestratorInstanceId,
                                          false,
                                          408,
                                          "action timeout")
                    ));
                }
                else
                {
                    // log step error, stop exe
                    logTasks.Add(context.CallActivityAsync(
                        "LogStep",
                        new LogStepEntity(false,
                                          projectRun.ProjectName,
                                          logRowKey,
                                          httpCallWithRetries.RowKey,
                                          projectRun.OrchestratorInstanceId,
                                          false,
                                          500,
                                          ex.Message)
                    ));
                }

                await Task.WhenAll(logTasks);

                throw ex;
            }
        }
    }
}
