using Microflow.Helpers;
using Microflow.MicroflowTableModels;
using Microflow.Models;
using MicroflowModels;
using MicroflowModels.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static MicroflowModels.Constants.Constants;

namespace Microflow.FlowControl
{
    public class MicroflowContext : IMicroflowContext
    {
        public IHttpCallWithRetries HttpCallWithRetries { get; set; }

        private IDurableOrchestrationContext MicroflowDurableContext { get; set; }
        private IMicroflowRun MicroflowRun { get; set; }
        private IMicroflowHttpResponse MicroflowHttpResponse { get; set; }
        private IList<Task> MicroflowTasks { get; set; }
        private ILogger Logger { get; set; }
        private string LogRowKey { get; }

        /// <summary>
        /// MicroflowContext contructor creates all needed class properties used in step execution
        /// </summary>
        public MicroflowContext(IDurableOrchestrationContext microflowContext,
                                IMicroflowRun workflowRun,
                                ILogger logger)
        {
            MicroflowDurableContext = microflowContext;
            MicroflowRun = workflowRun;
            LogRowKey = TableHelper.GetTableLogRowKeyDescendingByDate(MicroflowDurableContext.CurrentUtcDateTime, MicroflowDurableContext.NewGuid().ToString());
            MicroflowTasks = new List<Task>();
            Logger = MicroflowDurableContext.CreateReplaySafeLogger(logger);
        }

        /// <summary>
        /// Main entry point for step execution
        /// </summary>
        [Deterministic]
        public async Task RunMicroflow()
        {
            // get the step data from table storage (from PrepareWorkflow)
            await GetHttpCall();

            #region Region optional: no scale groups
#if !DEBUG_NOUPSERT_NOFLOWCONTROL_NOSCALEGROUPS && !DEBUG_NOUPSERT_NOSCALEGROUPS
            if (!string.IsNullOrWhiteSpace(HttpCallWithRetries.ScaleGroupId))
            {
                EntityId scaleId = new EntityId(ScaleGroupCalls.ScaleGroupMaxConcurrentInstanceCount, HttpCallWithRetries.ScaleGroupId);

                CanExecuteNowObject canExeNow = new CanExecuteNowObject()
                {
                    ScaleGroupId = HttpCallWithRetries.ScaleGroupId,
                    RunId = MicroflowRun.RunObject.RunId,
                    StepNumber = MicroflowRun.RunObject.StepNumber
                };

                await MicroflowDurableContext.CallSubOrchestratorAsync(ScaleGroupCalls.CanExecuteNowInScaleGroup, canExeNow);
            }
#endif
            #endregion

#if !DEBUG_NOUPSERT_NOFLOWCONTROL_NOSCALEGROUPS && !DEBUG_NOUPSERT_NOFLOWCONTROL
            EntityId projStateId = new EntityId(MicroflowStateKeys.WorkflowState, MicroflowRun.WorkflowName);
            EntityId globalStateId = new EntityId(MicroflowStateKeys.GlobalState, MicroflowRun.RunObject.GlobalKey);
            Task<int> projStateTask = MicroflowDurableContext.CallEntityAsync<int>(projStateId, MicroflowControlKeys.Read);
            Task<int> globalSateTask = MicroflowDurableContext.CallEntityAsync<int>(globalStateId, MicroflowControlKeys.Read);
            int projState = await projStateTask;
            int globalState = await globalSateTask;

            // check workflow and global states, run step if both states are ready
            if (projState == MicroflowStates.Ready && globalState == MicroflowStates.Ready)
            {
                // call out to micro-services orchestration
                await RunMicroflowStep();
            }
            // if workflow or global key state is paused, then pause this step, and wait and poll states by timer
            else if (projState == MicroflowStates.Paused || globalState == MicroflowStates.Paused)
            {
                // 7 days in paused state till exit
                DateTime endDate = MicroflowDurableContext.CurrentUtcDateTime.AddDays(7);
                // start interval seconds
                int count = 15;
                // max interval seconds
                const int max = 300; // 5 mins

                using (CancellationTokenSource cts = new CancellationTokenSource())
                {
                    try
                    {
                        while (MicroflowDurableContext.CurrentUtcDateTime < endDate)
                        {
                            DateTime deadline = MicroflowDurableContext.CurrentUtcDateTime.Add(TimeSpan.FromSeconds(count < max ? count : max));
                            await MicroflowDurableContext.CreateTimer(deadline, cts.Token);
                            count++;

                            // timer wait completed, refresh pause states
                            projStateTask = MicroflowDurableContext.CallEntityAsync<int>(projStateId, MicroflowControlKeys.Read);
                            globalSateTask = MicroflowDurableContext.CallEntityAsync<int>(globalStateId, MicroflowControlKeys.Read);
                            projState = await projStateTask;
                            globalState = await globalSateTask;

                            // check pause states, exit while if not paused
                            if (projState != MicroflowStates.Paused && globalState != MicroflowStates.Paused)
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

                // if workflow and global key state is ready, then continue to run step
                if (projState == MicroflowStates.Ready && globalState == MicroflowStates.Ready)
                {
                    // call out to micro-services orchestration
                    await RunMicroflowStep();
                }
            }
#else
            await RunMicroflowStep();
#endif
        }

        /// <summary>
        /// Do the step callout and process sub steps
        /// </summary>
        [Deterministic]
        private async Task RunMicroflowStep()
        {
            try
            {
                string id = MicroflowDurableContext.NewGuid().ToString();

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
                    await HttpCallout(id);
                }

#if !DEBUG_NOUPSERT_NOFLOWCONTROL_NOSCALEGROUPS && !DEBUG_NOUPSERT_NOSCALEGROUPS
                if (!string.IsNullOrWhiteSpace(HttpCallWithRetries.ScaleGroupId))
                {
                    EntityId countId = new EntityId(ScaleGroupCalls.CanExecuteNowInScaleGroupCount, HttpCallWithRetries.ScaleGroupId);

                    await MicroflowDurableContext.CallEntityAsync(countId, MicroflowCounterKeys.Subtract);
                }
#endif

                MicroflowTasks.Add(ProcessSubSteps());

                await Task.WhenAll(MicroflowTasks);
            }
            catch (Exception ex)
            {
                await HandleCalloutException(ex);
            }
        }

        /// <summary>
        /// Do the http callout
        /// </summary>
        private async Task HttpCallout(string id)
        {
            // call out to micro-service
            // wait for external event flow / callback
            if (!string.IsNullOrWhiteSpace(HttpCallWithRetries.CallbackAction))
            {
                const string name = "HttpCallWithCallbackOrchestrator";

                if (HttpCallWithRetries.RetryDelaySeconds > 0)
                {
                    MicroflowHttpResponse = await MicroflowDurableContext.CallSubOrchestratorWithRetryAsync<MicroflowHttpResponse>(name,
                                                                                                                                   HttpCallWithRetries.GetRetryOptions(),
                                                                                                                                   id,
                                                                                                                                   HttpCallWithRetries);
                }

                MicroflowHttpResponse = await MicroflowDurableContext.CallSubOrchestratorAsync<MicroflowHttpResponse>(name, id, HttpCallWithRetries);
            }
            // send and receive inline flow
            else
            {
                const string name = "HttpCallOrchestrator";

                if (HttpCallWithRetries.RetryDelaySeconds > 0)
                {
                    MicroflowHttpResponse = await MicroflowDurableContext.CallSubOrchestratorWithRetryAsync<MicroflowHttpResponse>(name,
                                                                                                                                   HttpCallWithRetries.GetRetryOptions(),
                                                                                                                                   id,
                                                                                                                                   HttpCallWithRetries);
                }

                MicroflowHttpResponse = await MicroflowDurableContext.CallSubOrchestratorAsync<MicroflowHttpResponse>(name, id, HttpCallWithRetries);
            }
        }

        /// <summary>
        /// Get and set the HttpCallWithRetries from table storage
        /// </summary>
        private async Task GetHttpCall()
        {
            HttpCallWithRetries = await MicroflowDurableContext.CallActivityAsync<HttpCallWithRetries>(CallNames.GetStep, MicroflowRun);

            HttpCallWithRetries.RunId = MicroflowRun.RunObject.RunId;
            HttpCallWithRetries.MainOrchestrationId = MicroflowRun.OrchestratorInstanceId;
            HttpCallWithRetries.GlobalKey = MicroflowRun.RunObject.GlobalKey;
        }

        /// <summary>
        /// This method will do the 1st sub steps check,
        /// if substep parentCount = 1 call "ExecuteStep" immediately,
        /// else call "CanExecuteNow" to check concurrent parentCountCompleted
        /// </summary>
        [Deterministic]
        private async Task ProcessSubSteps()
        {
            if (MicroflowHttpResponse.Success || !HttpCallWithRetries.StopOnActionFailed)
            {
                LogStepEnd();

                List<Task<CanExecuteResult>> canExecuteTasks = CanExecute();

                await ProcessStepCanExecuteTasks(canExecuteTasks);
            }
            else
            {
                LogStepFail();
            }
        }

        /// <summary>
        /// Process the CanExecute tasks
        /// </summary>
        private List<Task<CanExecuteResult>> CanExecute()
        {
            string[] stepsAndCounts = HttpCallWithRetries.SubSteps.Split(Splitter, StringSplitOptions.RemoveEmptyEntries);

            List<Task<CanExecuteResult>> canExecuteTasks = new List<Task<CanExecuteResult>>();

            for (int i = 0; i < stepsAndCounts.Length; i += 2)
            {
                // check parentCount
                // execute immediately if parentCount is 1
                int parentCount = Convert.ToInt32(stepsAndCounts[i + 1]);

                if (parentCount < 2)
                {
                    // stepsAndCounts[i] is stepNumber, stepsAndCounts[i + 1] is parentCount
                    MicroflowRun.RunObject = new RunObject()
                    {
                        RunId = MicroflowRun.RunObject.RunId,
                        StepNumber = stepsAndCounts[i],
                        GlobalKey = MicroflowRun.RunObject.GlobalKey
                    };

                    MicroflowTasks.Add(MicroflowDurableContext.CallSubOrchestratorAsync(CallNames.ExecuteStep, MicroflowRun));
                }
                // if parentCount is more than 1, work out if it can execute now
                else
                {
                    canExecuteTasks.Add(MicroflowDurableContext.CallSubOrchestratorAsync<CanExecuteResult>(CallNames.CanExecuteNow, new CanExecuteNowObject()
                    {
                        RunId = MicroflowRun.RunObject.RunId,
                        StepNumber = stepsAndCounts[i],
                        ParentCount = parentCount,
                        WorkflowName = MicroflowRun.WorkflowName
                    }));
                }
            }

            return canExecuteTasks;
        }

        /// <summary>
        /// Wait for the subStep canExeccuteTasks and process each result,
        /// if true call "ExecuteStep", else discard the subStep/ignore
        /// </summary>
        [Deterministic]
        private async Task ProcessStepCanExecuteTasks(IList<Task<CanExecuteResult>> canExecuteTasks)
        {
            // when a task compltes, process it and remove it from the task list
            while (canExecuteTasks.Count > 0)
            {
                Task<CanExecuteResult> canExecuteTask = await Task.WhenAny(canExecuteTasks);
                CanExecuteResult result = canExecuteTask.Result;

                if (result.CanExecute)
                {
                    MicroflowRun.RunObject = new RunObject()
                    {
                        RunId = MicroflowRun.RunObject.RunId,
                        StepNumber = result.StepNumber,
                        GlobalKey = MicroflowRun.RunObject.GlobalKey
                    };

                    MicroflowTasks.Add(MicroflowDurableContext.CallSubOrchestratorAsync(CallNames.ExecuteStep, MicroflowRun));
                }

                canExecuteTasks.Remove(canExecuteTask);
            }
        }

        /// <summary>
        /// Durable entity to keep an in progress count for each concurrent step in the workflow/run
        /// Used by HttpCallOrchestrator and HttpCallWithCallbackOrchestrator
        /// </summary>
        [FunctionName(MicroflowEntities.StepCount)]
        public static void StepCounter([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName)
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
        [Deterministic]
        private void LogStepStart()
        {
            MicroflowTasks.Add(MicroflowDurableContext.CallActivityAsync(
                CallNames.LogStep,
                new LogStepEntity(true,
                                  MicroflowRun.WorkflowName,
                                  LogRowKey,
                                  Convert.ToInt32(HttpCallWithRetries.RowKey),
                                  MicroflowRun.OrchestratorInstanceId,
                                  MicroflowRun.RunObject.RunId,
                                  MicroflowRun.RunObject.GlobalKey)
            ));
        }

        /// <summary>
        /// Log the end of the step
        /// </summary>
        [Deterministic]
        private void LogStepEnd()
        {
            MicroflowTasks.Add(MicroflowDurableContext.CallActivityAsync(
                CallNames.LogStep,
                new LogStepEntity(false,
                                  MicroflowRun.WorkflowName,
                                  LogRowKey,
                                  Convert.ToInt32(HttpCallWithRetries.RowKey),
                                  MicroflowRun.OrchestratorInstanceId,
                                  MicroflowRun.RunObject.RunId,
                                  MicroflowRun.RunObject.GlobalKey,
                                  MicroflowHttpResponse.Success,
                                  MicroflowHttpResponse.HttpResponseStatusCode,
                                  string.IsNullOrWhiteSpace(MicroflowHttpResponse.Message) ? null : MicroflowHttpResponse.Message)
            ));

            Logger.LogWarning($"Step {HttpCallWithRetries.RowKey} done at {MicroflowDurableContext.CurrentUtcDateTime.ToString("HH:mm:ss")}  -  Run ID: {MicroflowRun.RunObject.RunId}");
        }

        /// <summary>
        /// Log a fail of the step
        /// </summary>
        [Deterministic]
        private void LogStepFail()
        {
            Logger.LogError($"Step {HttpCallWithRetries.RowKey} failed at {MicroflowDurableContext.CurrentUtcDateTime.ToString("HH:mm:ss")}  -  Run ID: {MicroflowRun.RunObject.RunId}");

            MicroflowTasks.Add(MicroflowDurableContext.CallActivityAsync(
                CallNames.LogStep,
                new LogStepEntity(false,
                                  MicroflowRun.WorkflowName,
                                  LogRowKey,
                                  Convert.ToInt32(HttpCallWithRetries.RowKey),
                                  MicroflowRun.OrchestratorInstanceId,
                                  MicroflowRun.RunObject.RunId,
                                  MicroflowRun.RunObject.GlobalKey,
                                  false,
                                  MicroflowHttpResponse.HttpResponseStatusCode,
                                  string.IsNullOrWhiteSpace(MicroflowHttpResponse.Message) ? null : MicroflowHttpResponse.Message)
            ));
        }

        /// <summary>
        /// Handle the step execution exception
        /// </summary>
        [Deterministic]
        private async Task HandleCalloutException(Exception ex)
        {
            Logger.LogWarning($"Step {HttpCallWithRetries.RowKey} an error result at {MicroflowDurableContext.CurrentUtcDateTime.ToString("HH:mm:ss")}  -  Run ID: {MicroflowRun.RunObject.RunId}");

            if (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            if (ex is TimeoutException)
            {
                MicroflowTasks.Add(MicroflowDurableContext.CallActivityAsync(
                    CallNames.LogStep,
                    new LogStepEntity(false,
                                      MicroflowRun.WorkflowName,
                                      LogRowKey,
                                      Convert.ToInt32(HttpCallWithRetries.RowKey),
                                      MicroflowRun.OrchestratorInstanceId,
                                      MicroflowRun.RunObject.RunId,
                                      MicroflowRun.RunObject.GlobalKey,
                                      false,
                                      -408,
                                      string.IsNullOrWhiteSpace(HttpCallWithRetries.CallbackAction)
                                        ? "callout timeout"
                                        : $"action {HttpCallWithRetries.CallbackAction} timed out, StopOnActionFailed is {HttpCallWithRetries.StopOnActionFailed}")
                ));
            }
            else
            {
                MicroflowTasks.Add(MicroflowDurableContext.CallActivityAsync(
                    CallNames.LogStep,
                    new LogStepEntity(false,
                                      MicroflowRun.WorkflowName,
                                      LogRowKey,
                                      Convert.ToInt32(HttpCallWithRetries.RowKey),
                                      MicroflowRun.OrchestratorInstanceId,
                                      MicroflowRun.RunObject.RunId,
                                      MicroflowRun.RunObject.GlobalKey,
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
