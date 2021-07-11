using Microflow.Helpers;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microflow.FlowControl
{
    public class MicroflowContext : IMicroflowContext
    {
        public IHttpCallWithRetries HttpCallWithRetries { get => _httpCallWithRetries; set => _httpCallWithRetries = value; }
        public IHttpCallWithRetries _httpCallWithRetries;

        private IDurableOrchestrationContext MicroflowDurableContext { get => _microflowContext; set => _microflowContext = value; }
        private IProjectRun ProjectRun { get => _projectRun; set => _projectRun = value; }
        private string LogRowKey => _logRowKey;
        private IMicroflowHttpResponse MicroflowHttpResponse { get => _microflowHttpResponse; set => _microflowHttpResponse = value; }
        private IList<Task> MicroflowTasks { get => _microFlowTasks; set => _microFlowTasks = value; }
        private ILogger Logger { get => _logger; set => _logger = value; }

        private IDurableOrchestrationContext _microflowContext;
        private IProjectRun _projectRun;
        private IMicroflowHttpResponse _microflowHttpResponse;
        private IList<Task> _microFlowTasks;
        private ILogger _logger;
        private readonly string _logRowKey;

        public MicroflowContext(IDurableOrchestrationContext microflowContext, IProjectRun projectRun, ILogger logger)
        {
            MicroflowDurableContext = microflowContext;
            ProjectRun = projectRun;
            _logRowKey = MicroflowTableHelper.GetTableLogRowKeyDescendingByDate(MicroflowDurableContext.CurrentUtcDateTime, MicroflowDurableContext.NewGuid().ToString());
            MicroflowTasks = new List<Task>();
            Logger = MicroflowDurableContext.CreateReplaySafeLogger(logger);
        }

        public async Task RunMicroflow()
        {
            EntityId projStateId = new EntityId(MicroflowStateKeys.ProjectStateId, ProjectRun.ProjectName);
            EntityId globalStateId = new EntityId(MicroflowStateKeys.GlobalStateId, ProjectRun.RunObject.GlobalKey);
            Task<int> projStateTask = MicroflowDurableContext.CallEntityAsync<int>(projStateId, MicroflowControlKeys.Read);
            Task<int> globalSateTask = MicroflowDurableContext.CallEntityAsync<int>(globalStateId, MicroflowControlKeys.Read);
            int projState = await projStateTask;
            int globalState = await globalSateTask;

            if (projState == 0 && globalState == 0)
            {
                // call out to micro-services orchestration
                await RunMicroflowStep();
            }
            else if (projState == 1 || globalState == 1)
            {
                DateTime endDate = MicroflowDurableContext.CurrentUtcDateTime.AddDays(7);
                int count = 15;
                int max = 60;

                using (CancellationTokenSource cts = new CancellationTokenSource())
                {
                    try
                    {
                        while (MicroflowDurableContext.CurrentUtcDateTime < endDate)
                        {
                            //context.SetCustomStatus("paused");

                            DateTime deadline = MicroflowDurableContext.CurrentUtcDateTime.Add(TimeSpan.FromSeconds(count < max ? count : max));
                            await MicroflowDurableContext.CreateTimer(deadline, cts.Token);
                            count++;

                            projStateTask = MicroflowDurableContext.CallEntityAsync<int>(projStateId, MicroflowControlKeys.Read);
                            globalSateTask = MicroflowDurableContext.CallEntityAsync<int>(globalStateId, MicroflowControlKeys.Read);
                            projState = await projStateTask;
                            globalState = await globalSateTask;

                            if (projState != 1 && globalState != 1)
                            {
                                break;
                            }
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        Logger.LogCritical("========================TaskCanceledException==========================");
                    }
                    finally
                    {
                        cts.Dispose();
                    }
                }

                //context.SetCustomStatus("running");
                if (projState == 0 && globalState == 0)
                {
                    // call out to micro-services orchestration
                    await RunMicroflowStep();
                }
            }
        }

        /// <summary>
        /// Do the step callout and process sub steps
        /// </summary>
        private async Task RunMicroflowStep()
        {
            try
            {
                // get the step data from table storage (from PrepareWorkflow)
                HttpCallWithRetries = await MicroflowDurableContext.CallActivityAsync<HttpCallWithRetries>("GetStep", ProjectRun);

                string id = MicroflowDurableContext.NewGuid().ToString();
                HttpCallWithRetries.RunId = ProjectRun.RunObject.RunId;
                HttpCallWithRetries.MainOrchestrationId = ProjectRun.OrchestratorInstanceId;
                HttpCallWithRetries.GlobalKey = ProjectRun.RunObject.GlobalKey;

                // log start of step
                LogStepStart();

                // if the callout url is empty then no http call is done, use this for an empty container step
                if (string.IsNullOrWhiteSpace(HttpCallWithRetries.CalloutUrl))
                {
                    MicroflowHttpResponse = new MicroflowHttpResponse
                    {
                        Success = true,
                        HttpResponseStatusCode = -404
                    };
                }
                else
                {
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
                }

                MicroflowTasks.Add(ProcessSubSteps());

                await Task.WhenAll(MicroflowTasks);
            }
            catch (Exception ex)
            {
                await HandleCalloutException(ex);
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

                string[] stepsAndCounts = HttpCallWithRetries.SubSteps.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                List<Task<CanExecuteResult>> canExecuteTasks = new List<Task<CanExecuteResult>>();

                for (int i = 0; i < stepsAndCounts.Length; i = i + 2)
                {
                    // check parentCount
                    // execute immediately if parentCount is 1
                    int parentCount = Convert.ToInt32(stepsAndCounts[i + 1]);

                    if (parentCount < 2)
                    {
                        // stepsAndCounts[i] is stepNumber, stepsAndCounts[i + 1] is parentCount
                        ProjectRun.RunObject = new RunObject() { 
                            RunId = ProjectRun.RunObject.RunId, 
                            StepNumber = stepsAndCounts[i],
                            GlobalKey = ProjectRun.RunObject.GlobalKey
                        };

                        MicroflowTasks.Add(MicroflowDurableContext.CallSubOrchestratorAsync("ExecuteStep", ProjectRun));
                    }
                    // if parentCount is more than 1, work out if it can execute now
                    else
                    {
                        canExecuteTasks.Add(MicroflowDurableContext.CallSubOrchestratorAsync<CanExecuteResult>("CanExecuteNow", new CanExecuteNowObject()
                        {
                            RunId = ProjectRun.RunObject.RunId,
                            StepNumber = stepsAndCounts[i],
                            ParentCount = parentCount,
                            ProjectName = ProjectRun.ProjectName
                        }));
                    }
                }

                await ProcessStepCanExecuteTasks(canExecuteTasks);
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
                        StepNumber = result.StepNumber,
                        GlobalKey = ProjectRun.RunObject.GlobalKey
                    };

                    MicroflowTasks.Add(MicroflowDurableContext.CallSubOrchestratorAsync("ExecuteStep", ProjectRun));
                }

                canExecuteTasks.Remove(canExecuteTask);
            }
        }

        /// <summary>
        /// Durable entity to keep an in progress count for each concurrent step in the project/run
        /// Used by HttpCallOrchestrator and HttpCallWithCallbackOrchestrator
        /// </summary>
        [FunctionName(MicroflowEntities.StepCounter)]
        public static void StepCounter([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName.ToLowerInvariant())
            {
                case MicroflowCounterKeys.Add:
                    ctx.SetState(ctx.GetState<int>() + 1);
                    break;
                //case "reset":
                //    ctx.SetState(0);
                //    break;
                case MicroflowCounterKeys.Read:
                    ctx.Return(ctx.GetState<int>());
                    break;
                case MicroflowCounterKeys.Subtract:
                    ctx.SetState(ctx.GetState<int>() - 1);
                    break;
                    //case "delete":
                    //    ctx.DeleteState();
                    //    break;
            }
        }

        /// <summary>
        /// Log the start of the step
        /// </summary>
        private void LogStepStart()
        {
            MicroflowTasks.Add(MicroflowDurableContext.CallActivityAsync(
                "LogStep",
                new LogStepEntity(true,
                                  ProjectRun.ProjectName,
                                  LogRowKey,
                                  Convert.ToInt32(HttpCallWithRetries.RowKey),
                                  ProjectRun.OrchestratorInstanceId,
                                  ProjectRun.RunObject.RunId,
                                  ProjectRun.RunObject.GlobalKey)
            ));
        }

        /// <summary>
        /// Log the end of the step
        /// </summary>
        private void LogStepEnd()
        {
            MicroflowTasks.Add(MicroflowDurableContext.CallActivityAsync(
                "LogStep",
                new LogStepEntity(false,
                                  ProjectRun.ProjectName,
                                  LogRowKey,
                                  Convert.ToInt32(HttpCallWithRetries.RowKey),
                                  ProjectRun.OrchestratorInstanceId,
                                  ProjectRun.RunObject.RunId,
                                  ProjectRun.RunObject.GlobalKey,
                                  MicroflowHttpResponse.Success,
                                  MicroflowHttpResponse.HttpResponseStatusCode,
                                  string.IsNullOrWhiteSpace(MicroflowHttpResponse.Message) ? null : MicroflowHttpResponse.Message)
            ));

            Logger.LogWarning($"Step {HttpCallWithRetries.RowKey} done at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {ProjectRun.RunObject.RunId}");
        }

        /// <summary>
        /// Log a fail of the step
        /// </summary>
        private void LogStepFail()
        {
            Logger.LogError($"Step {HttpCallWithRetries.RowKey} failed at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {ProjectRun.RunObject.RunId}");

            MicroflowTasks.Add(MicroflowDurableContext.CallActivityAsync(
                "LogStep",
                new LogStepEntity(false,
                                  ProjectRun.ProjectName,
                                  LogRowKey,
                                  Convert.ToInt32(HttpCallWithRetries.RowKey),
                                  ProjectRun.OrchestratorInstanceId,
                                  ProjectRun.RunObject.RunId,
                                  ProjectRun.RunObject.GlobalKey,
                                  false,
                                  MicroflowHttpResponse.HttpResponseStatusCode,
                                  string.IsNullOrWhiteSpace(MicroflowHttpResponse.Message) ? null : MicroflowHttpResponse.Message)
            ));
        }

        /// <summary>
        /// Handle the step execution exception
        /// </summary>
        private async Task HandleCalloutException(Exception ex)
        {
            Logger.LogWarning($"Step {HttpCallWithRetries.RowKey} an error result at {DateTime.Now.ToString("HH:mm:ss")}  -  Run ID: {ProjectRun.RunObject.RunId}");

            if (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            if (ex is TimeoutException)
            {
                MicroflowTasks.Add(MicroflowDurableContext.CallActivityAsync(
                    "LogStep",
                    new LogStepEntity(false,
                                      ProjectRun.ProjectName,
                                      LogRowKey,
                                      Convert.ToInt32(HttpCallWithRetries.RowKey),
                                      ProjectRun.OrchestratorInstanceId,
                                      ProjectRun.RunObject.RunId,
                                      ProjectRun.RunObject.GlobalKey,
                                      false,
                                      -408,
                                      string.IsNullOrWhiteSpace(HttpCallWithRetries.CallBackAction) 
                                        ? "action timeout" 
                                        : $"action {HttpCallWithRetries.CallBackAction} timed out, StopOnActionFailed is {HttpCallWithRetries.StopOnActionFailed}")
                ));
            }
            else
            {
                MicroflowTasks.Add(MicroflowDurableContext.CallActivityAsync(
                    "LogStep",
                    new LogStepEntity(false,
                                      ProjectRun.ProjectName,
                                      LogRowKey,
                                      Convert.ToInt32(HttpCallWithRetries.RowKey),
                                      ProjectRun.OrchestratorInstanceId,
                                      ProjectRun.RunObject.RunId,
                                      ProjectRun.RunObject.GlobalKey,
                                      false,
                                      -500,
                                      ex.Message)
                ));

                await Task.WhenAll(MicroflowTasks);

                throw ex;
            }
        }
    }
}
