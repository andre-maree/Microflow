using Microflow.Helpers;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microflow.FlowControl
{
    public class MicroflowContext : IMicroflowContext
    {
        public IHttpCallWithRetries HttpCallWithRetries { get => _httpCallWithRetries; set => _httpCallWithRetries = value; }
        private IDurableOrchestrationContext MicroflowDurableContext { get => _microflowContext; set => _microflowContext = value; }
        private IProjectRun ProjectRun { get => _projectRun; set => _projectRun = value; }
        private string LogRowKey => _logRowKey;
        private IMicroflowHttpResponse MicroflowHttpResponse { get => _microflowHttpResponse; set => _microflowHttpResponse = value; }
        private IList<Task> SubStepTasks { get => _subStepTasks; set => _subStepTasks = value; }
        private IList<Task> LogTasks { get => _logTasks; set => _logTasks = value; }
        private ILogger Logger { get => _logger; set => _logger = value; }

        private IDurableOrchestrationContext _microflowContext;
        private IProjectRun _projectRun;
        public IHttpCallWithRetries _httpCallWithRetries;
        private IMicroflowHttpResponse _microflowHttpResponse;
        private IList<Task> _subStepTasks;
        private IList<Task> _logTasks;
        private ILogger _logger;
        private readonly string _logRowKey;

        public MicroflowContext(IDurableOrchestrationContext microflowContext, IProjectRun projectRun, ILogger logger)
        {
            MicroflowDurableContext = microflowContext;
            ProjectRun = projectRun;
            _logRowKey = MicroflowTableHelper.GetTableLogRowKeyDescendingByDate(MicroflowDurableContext.CurrentUtcDateTime, MicroflowDurableContext.NewGuid().ToString());
            SubStepTasks = new List<Task>();
            LogTasks = new List<Task>();
            Logger = MicroflowDurableContext.CreateReplaySafeLogger(logger);
        }

        /// <summary>
        /// Do the step callout and process sub steps
        /// </summary>
        public async Task RunMicroflow()
        {
            try
            {
                // get the step data from table storage (from PrepareWorkflow)
                Task<HttpCallWithRetries> httpCallWithRetriesTask = MicroflowDurableContext.CallActivityAsync<HttpCallWithRetries>("GetStep", ProjectRun);

                HttpCallWithRetries = await httpCallWithRetriesTask;

                EntityId countId = new EntityId("StepCounter", HttpCallWithRetries.PartitionKey + HttpCallWithRetries.RowKey);

                // set the per step in-progress count to count+1
                MicroflowDurableContext.SignalEntity(countId, "add");

                string id = MicroflowDurableContext.NewGuid().ToString();
                HttpCallWithRetries.RunId = ProjectRun.RunObject.RunId;
                HttpCallWithRetries.MainOrchestrationId = ProjectRun.OrchestratorInstanceId;

                // log start of step
                LogStepStart();

                // if the call out url is empty then no http call is done, use this for an empty container step
                if (string.IsNullOrWhiteSpace(HttpCallWithRetries.CalloutUrl))
                {
                    MicroflowHttpResponse = new MicroflowHttpResponse();
                    MicroflowHttpResponse.Success = true;
                    MicroflowHttpResponse.HttpResponseStatusCode = -1;
                }

                // call out to micro-service
                // wait for external event flow / callback
                if (!string.IsNullOrWhiteSpace(HttpCallWithRetries.CallBackAction))
                {
                    const string name = "HttpCallWithCallBackOrchestrator";

                    if (HttpCallWithRetries.RetryDelaySeconds > 0)
                    {
                        MicroflowHttpResponse = await MicroflowDurableContext.CallSubOrchestratorWithRetryAsync<MicroflowHttpResponse>(name, HttpCallWithRetries.GetRetryOptions(), id, HttpCallWithRetries);
                    }

                    MicroflowHttpResponse = await MicroflowDurableContext.CallSubOrchestratorAsync<MicroflowHttpResponse>(name, id, HttpCallWithRetries);
                }
                // send and receive inline flow
                else
                {
                    const string name = "HttpCallOrchestrator";

                    if (HttpCallWithRetries.RetryDelaySeconds > 0)
                    {
                        MicroflowHttpResponse = await MicroflowDurableContext.CallSubOrchestratorWithRetryAsync<MicroflowHttpResponse>(name, HttpCallWithRetries.GetRetryOptions(), id, HttpCallWithRetries);
                    }

                    MicroflowHttpResponse = await MicroflowDurableContext.CallSubOrchestratorAsync<MicroflowHttpResponse>(name, id, HttpCallWithRetries);
                }

                // set the per step in-progress count to count-1
                MicroflowDurableContext.SignalEntity(countId, "subtract");

                SubStepTasks.Add(ProcessSubSteps());

                await Task.WhenAll(LogTasks);

                await Task.WhenAll(SubStepTasks);
            }
            catch (Exception ex)
            {
                MicroflowHttpResponse = await HandleCalloutException(ex);
            }
        }

        /// <summary>
        /// This method will do the 1st sub steps check,
        /// if substep parentCount = 1 call "ExecuteStep" immediately,
        /// else call "CanExecuteNow" to check concurrent parentCountCompleted
        /// </summary>
        private async Task ProcessSubSteps()
        {
            if (MicroflowHttpResponse.Success || !HttpCallWithRetries.StopOnActionFailed)
            {
                LogStepEnd();

                List<List<int>> subSteps = JsonSerializer.Deserialize<List<List<int>>>(HttpCallWithRetries.SubSteps);

                var canExeccuteTasks = new List<Task<CanExecuteResult>>();

                foreach (var step in subSteps)
                {
                    // check parentCount
                    // execute immediately if parentCount is 1
                    if (step[1] < 2)
                    {
                        // step[0] is stepId, step[1] is parentCount
                        ProjectRun.RunObject = new RunObject() { RunId = ProjectRun.RunObject.RunId, StepId = step[0] };

                        SubStepTasks.Add(MicroflowDurableContext.CallSubOrchestratorAsync("ExecuteStep", ProjectRun));
                    }
                    // if parentCount is more than 1, work out if it can execute now
                    else
                    {
                        canExeccuteTasks.Add(MicroflowDurableContext.CallSubOrchestratorAsync<CanExecuteResult>("CanExecuteNow", new CanExecuteNowObject()
                        {
                            RunId = ProjectRun.RunObject.RunId,
                            StepId = step[0],
                            ParentCount = step[1],
                            ProjectName = ProjectRun.ProjectName
                        }));
                    }
                }

                await ProcessStepCanExecuteTasks(canExeccuteTasks);
            }
            else
            {
                LogStepFail();
            }
        }

        /// <summary>
        /// Wait for the subStep canExeccuteTasks and process each result,
        /// if true call "ExecuteStep", else discard the subStep/ignore
        /// </summary>
        private async Task ProcessStepCanExecuteTasks(IList<Task<CanExecuteResult>> canExecuteTasks)
        {
            for (int i = 0; i < canExecuteTasks.Count;)
            {
                Task<CanExecuteResult> canExecuteTask = await Task.WhenAny(canExecuteTasks);
                CanExecuteResult result = canExecuteTask.Result;

                if (result.CanExecute)
                {
                    ProjectRun.RunObject = new RunObject()
                    {
                        RunId = ProjectRun.RunObject.RunId,
                        StepId = result.StepId
                    };

                    SubStepTasks.Add(MicroflowDurableContext.CallSubOrchestratorAsync("ExecuteStep", ProjectRun));
                }

                canExecuteTasks.Remove(canExecuteTask);
            }
        }

        /// <summary>
        /// Durable entity to keep an in progress count for each concurrent step in the project/run
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
        /// Log the start of step
        /// </summary>
        private void LogStepStart()
        {
            // log start of step
            LogTasks.Add(MicroflowDurableContext.CallActivityAsync(
                "LogStep",
                new LogStepEntity(true, ProjectRun.ProjectName, LogRowKey, HttpCallWithRetries.RowKey, ProjectRun.OrchestratorInstanceId)
            ));
        }

        /// <summary>
        /// Log the end of step
        /// </summary>
        private void LogStepEnd()
        {
            // log end of step
            LogTasks.Add(MicroflowDurableContext.CallActivityAsync(
                "LogStep",
                new LogStepEntity(false,
                                  ProjectRun.ProjectName,
                                  LogRowKey,
                                  HttpCallWithRetries.RowKey,
                                  ProjectRun.OrchestratorInstanceId,
                                  MicroflowHttpResponse.Success,
                                  MicroflowHttpResponse.HttpResponseStatusCode,
                                  string.IsNullOrWhiteSpace(MicroflowHttpResponse.Message) ? null : MicroflowHttpResponse.Message)
            ));

            Logger.LogWarning($"Step {HttpCallWithRetries.RowKey} done at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {ProjectRun.RunObject.RunId}");
        }

        /// <summary>
        /// Log the fail of step
        /// </summary>
        private void LogStepFail()
        {
            Logger.LogError($"Step {HttpCallWithRetries.RowKey} failed at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {ProjectRun.RunObject.RunId}");

            // log step error, stop exe
            LogTasks.Add(MicroflowDurableContext.CallActivityAsync(
                "LogStep",
                new LogStepEntity(false,
                                  ProjectRun.ProjectName,
                                  LogRowKey,
                                  HttpCallWithRetries.RowKey,
                                  ProjectRun.OrchestratorInstanceId,
                                  false,
                                  MicroflowHttpResponse.HttpResponseStatusCode,
                                  string.IsNullOrWhiteSpace(MicroflowHttpResponse.Message) ? null : MicroflowHttpResponse.Message)
            ));
        }

        /// <summary>
        /// Handle the step execution exception
        /// </summary>
        private async Task<MicroflowHttpResponse> HandleCalloutException(Exception ex)
        {
            Logger.LogWarning($"Step {HttpCallWithRetries.RowKey} an error result at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {ProjectRun.RunObject.RunId}");

            if (!HttpCallWithRetries.StopOnActionFailed)
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
                    LogTasks.Add(MicroflowDurableContext.CallActivityAsync(
                        "LogStep",
                        new LogStepEntity(false,
                                          ProjectRun.ProjectName,
                                          LogRowKey,
                                          HttpCallWithRetries.RowKey,
                                          ProjectRun.OrchestratorInstanceId,
                                          false,
                                          408,
                                          "action timeout")
                    ));
                }
                else
                {
                    // log step error, stop exe
                    LogTasks.Add(MicroflowDurableContext.CallActivityAsync(
                        "LogStep",
                        new LogStepEntity(false,
                                          ProjectRun.ProjectName,
                                          LogRowKey,
                                          HttpCallWithRetries.RowKey,
                                          ProjectRun.OrchestratorInstanceId,
                                          false,
                                          500,
                                          ex.Message)
                    ));
                }

                await Task.WhenAll(LogTasks);

                throw ex;
            }
        }
    }
}
