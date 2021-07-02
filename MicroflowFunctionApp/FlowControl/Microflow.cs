using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.Models;
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
                                             ILogger inLog)
        {
            ILogger log = context.CreateReplaySafeLogger(inLog);

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

                string logRowKey = MicroflowTableHelper.GetTableLogRowKeyDescendingByDate(context.CurrentUtcDateTime, context.NewGuid().ToString());

                if (httpCallWithRetries.RetryDelaySeconds > 0)
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

                MicroflowHttpResponse microflowHttpResponse;
                var subTasks = new List<Task>();
                var logTasks = new List<Task>();

                EntityId countId = new EntityId(nameof(StepCounter), httpCallWithRetries.PartitionKey + httpCallWithRetries.RowKey);

                // set the per step in-progress count to count+1
                context.SignalEntity(countId, "add");

                // call out to micro-service
                microflowHttpResponse = await StepCallout(context, log, projectRun, runObj, httpCallWithRetries, retryOptions, logRowKey, logTasks);

                // set the per step in-progress count to count-1
                context.SignalEntity(countId, "subtract");

                await ProcessSubSteps(context, log, projectRun, runObj, httpCallWithRetries, logRowKey, microflowHttpResponse, subTasks, logTasks);

                await Task.WhenAll(logTasks);

                await Task.WhenAll(subTasks);
            }
            catch (Exception e)
            {
                int? stepId = httpCallWithRetries == null ? -1 : Convert.ToInt32(httpCallWithRetries.RowKey);

                // log to table workflow completed
                LogErrorEntity errorEntity = new LogErrorEntity(projectRun.ProjectName, e.Message, runObj.RunId, stepId);
                await context.CallActivityAsync("LogError", errorEntity);
            }
        }

        /// <summary>
        /// This method will do the 1st sub steps check,
        /// if substep parentCount = 1 call "ExecuteStep" immediately,
        /// else call "CanExecuteNow" to check concurrent parentCountCompleted
        /// </summary>
        private static async Task ProcessSubSteps(IDurableOrchestrationContext context,
                                                      ILogger log,
                                                      ProjectRun projectRun,
                                                      RunObject runObj,
                                                      HttpCallWithRetries httpCallWithRetries,
                                                      string logRowKey,
                                                      MicroflowHttpResponse microflowHttpResponse,
                                                      List<Task> subTasks,
                                                      List<Task> logTasks)
        {
            if (microflowHttpResponse.Success || !httpCallWithRetries.StopOnActionFailed)
            {
                LogStepEnd(context, log, projectRun, httpCallWithRetries, logRowKey, microflowHttpResponse, logTasks);

                List<List<int>> subSteps = JsonSerializer.Deserialize<List<List<int>>>(httpCallWithRetries.SubSteps);

                var canExeccuteTasks = new List<Task<CanExecuteResult>>();

                foreach (var step in subSteps)
                {
                    // check parentCount
                    // execute immediately if parentCount is 1
                    if (step[1] < 2)
                    {
                        // step[0] is stepId, step[1] is parentCount
                        projectRun.RunObject = new RunObject() { RunId = runObj.RunId, StepId = step[0] };

                        subTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", projectRun));
                    }
                    // if parentCount is more than 1, work out if it can execute now
                    else
                    {
                        canExeccuteTasks.Add(context.CallSubOrchestratorAsync<CanExecuteResult>("CanExecuteNow", new CanExecuteNowObject()
                        {
                            RunId = runObj.RunId,
                            StepId = step[0],
                            ParentCount = step[1],
                            ProjectName = projectRun.ProjectName
                        }));
                    }
                }

                await ProcessStepCanExecuteTasks(context, projectRun, runObj, subTasks, canExeccuteTasks);
            }
            else
            {
                LogStepFail(context, log, projectRun, httpCallWithRetries, logRowKey, microflowHttpResponse, logTasks);
            }
        }

        /// <summary>
        /// Wait for the subStep canExeccuteTasks and process each result,
        /// if true call "ExecuteStep", else discard the subStep/ignore
        /// </summary>
        private static async Task ProcessStepCanExecuteTasks(IDurableOrchestrationContext context,
                                                             ProjectRun projectRun,
                                                             RunObject runObj,
                                                             List<Task> subTasks,
                                                             List<Task<CanExecuteResult>> canExecuteTasks)
        {
            for (int i = 0; i < canExecuteTasks.Count;)
            {
                Task<CanExecuteResult> canExecuteTask = await Task.WhenAny(canExecuteTasks);
                CanExecuteResult result = canExecuteTask.Result;

                if (result.CanExecute)
                {
                    projectRun.RunObject = new RunObject()
                    {
                        RunId = runObj.RunId,
                        StepId = result.StepId
                    };

                    subTasks.Add(context.CallSubOrchestratorAsync("ExecuteStep", projectRun));
                }

                canExecuteTasks.Remove(canExecuteTask);
            }
        }

        /// <summary>
        /// Durable entity to keep a count for each run and each step in the run
        /// </summary>
        [FunctionName("StepCounter")]
        public static void StepCounter([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName.ToLowerInvariant())
            {
                case "add":
                    ctx.SetState(ctx.GetState<int>() + 1);
                    break;
                //case "reset":
                //    ctx.SetState(0);
                //    break;
                case "get":
                    ctx.Return(ctx.GetState<int>());
                    break;
                case "subtract":
                    ctx.SetState(ctx.GetState<int>() - 1);
                    break;
                //case "delete":
                //    ctx.DeleteState();
                //    break;
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
                                                                   List<Task> logTasks)
        {
            try
            {
                string id = context.NewGuid().ToString();

                httpCallWithRetries.RunId = runObj.RunId;
                httpCallWithRetries.MainOrchestrationId = projectRun.OrchestratorInstanceId;

                // log start of step
                LogStepStart(context, projectRun, httpCallWithRetries, logRowKey, logTasks);

                // if the call out url is empty then no http call is done, use this for an empty container step
                if (string.IsNullOrWhiteSpace(httpCallWithRetries.Url))
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = true,
                        HttpResponseStatusCode = -1
                    };
                }

                // wait for external event flow / callback
                if (!string.IsNullOrWhiteSpace(httpCallWithRetries.CallBackAction))
                {
                    const string name = "HttpCallWithCallBackOrchestrator";

                    if (retryOptions == null)
                    {
                        return await context.CallSubOrchestratorAsync<MicroflowHttpResponse>(name, id, httpCallWithRetries);
                    }

                    return await context.CallSubOrchestratorWithRetryAsync<MicroflowHttpResponse>(name, retryOptions, id, httpCallWithRetries);
                }
                // send and receive inline flow
                else
                {
                    const string name = "HttpCallOrchestrator";

                    if (retryOptions == null)
                    {
                        return await context.CallSubOrchestratorAsync<MicroflowHttpResponse>(name, id, httpCallWithRetries);
                    }

                    return await context.CallSubOrchestratorWithRetryAsync<MicroflowHttpResponse>(name, retryOptions, id, httpCallWithRetries);
                }
            }
            catch (Exception ex)
            {
                return await HandleCalloutException(context, log, projectRun, httpCallWithRetries, logRowKey, logTasks, ex);
            }
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
