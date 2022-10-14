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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static MicroflowModels.Constants;

namespace Microflow.FlowControl
{
    public class MicroflowContext : IMicroflowContext
    {
        public IHttpCallWithRetries HttpCallWithRetries { get; set; }

        private IDurableOrchestrationContext MicroflowDurableContext { get; set; }
        private IMicroflowRun MicroflowRun { get; set; }
        private MicroflowHttpResponse MicroflowHttpResponse { get; set; }
        private IList<Task> MicroflowTasks { get; set; }
        private ILogger Logger { get; set; }
        private string LogRowKey { get; }
        //private string SubInstanceId { get; set; }

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
            //SubInstanceId = MicroflowDurableContext.NewGuid().ToString();
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
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_SCALEGROUPS && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_SCALEGROUPS && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT
            ////////////////////////////////////////
            if (!string.IsNullOrWhiteSpace(HttpCallWithRetries.ScaleGroupId))
            {
                EntityId scaleId = new(ScaleGroupCalls.ScaleGroupMaxConcurrentInstanceCount, HttpCallWithRetries.ScaleGroupId);

                CanExecuteNowObject canExeNow = new()
                {
                    ScaleGroupId = HttpCallWithRetries.ScaleGroupId,
                    RunId = MicroflowRun.RunObject.RunId,
                    StepNumber = MicroflowRun.RunObject.StepNumber
                };

                await MicroflowDurableContext.CallSubOrchestratorAsync(ScaleGroupCalls.CanExecuteNowInScaleGroup, canExeNow);
            }
            ////////////////////////////////////////
#endif
            #endregion

            EntityId projStateId = new(MicroflowStateKeys.WorkflowState, MicroflowRun.WorkflowName);
            EntityId globalStateId = new(MicroflowStateKeys.GlobalState, MicroflowRun.RunObject.GlobalKey);
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
            else if (projState == MicroflowStates.Stopped || globalState == MicroflowStates.Stopped)
            {
                // do nothing and exit
            }
            // if workflow or global key state is paused, then pause this step, and wait and poll states by timer
            else if (projState == MicroflowStates.Paused || globalState == MicroflowStates.Paused)
            {

                DateTime endDate = MicroflowDurableContext.CurrentUtcDateTime.AddHours(PollingConfig.PollingMaxHours);
                // start interval seconds
                int count = PollingConfig.PollingIntervalSeconds;
                // max interval seconds
                int max = PollingConfig.PollingIntervalMaxSeconds;

                using (CancellationTokenSource cts = new())
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
                    catch (TaskCanceledException toex)
                    {
                        MicroflowHttpResponse.Content = toex.Message;
                        MicroflowHttpResponse.HttpResponseStatusCode = -408;
                        LogStepFail();
                    }
                    catch (Exception ex)
                    {
                        MicroflowHttpResponse.Content = ex.Message;
                        MicroflowHttpResponse.HttpResponseStatusCode = -500;
                        LogStepFail();
                    }
                    finally
                    {
                        cts.Dispose();
                    }
                }

                // if workflow and global key state is ready, then continue to run step
                if (projState == MicroflowStates.Stopped || globalState == MicroflowStates.Stopped)
                {
                    // Stopped flow will exit here without calling RunMicroflowStep()
                    return;
                }

                // recurse refresh
                await RunMicroflow();
            }
        }

        /// <summary>
        /// Do the step callout and process sub steps
        /// </summary>
        [Deterministic]
        private async Task RunMicroflowStep()
        {
            try
            {
                string subInstanceId = MicroflowDurableContext.NewGuid().ToString();

                MicroflowHttpHelper.ParseUrlMicroflowData(HttpCallWithRetries, subInstanceId);

                // log start of step
                LogStepStart(subInstanceId);

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
                    await HttpCallout(subInstanceId);
                }

                #region Region optional: no scale groups
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_SCALEGROUPS && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_SCALEGROUPS && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT
                //////////////////////////////////////////////
                if (!string.IsNullOrWhiteSpace(HttpCallWithRetries.ScaleGroupId))
                {
                    EntityId countId = new(ScaleGroupCalls.CanExecuteNowInScaleGroupCount, HttpCallWithRetries.ScaleGroupId);

                    await MicroflowDurableContext.CallEntityAsync(countId, MicroflowEntityKeys.Subtract);
                }
                //////////////////////////////////////////////
#endif
                #endregion

                MicroflowTasks.Add(ProcessSubSteps());

                await Task.WhenAll(MicroflowTasks);
            }
            catch (Exception ex)
            {
                if (ex.InnerException is TimeoutException tex)
                {
                    HandleWebhookTimeout(tex);

                    MicroflowTasks.Add(ProcessSubSteps());
                }
                else
                {
                    await HandleCalloutException(ex);
                }
            }
        }

        /// <summary>
        /// Do the http callout
        /// </summary>
        private async Task HttpCallout(string subInstanceId)
        {
            MicroflowHttpResponse runObjectResponseData = MicroflowRun.RunObject.MicroflowStepResponseData ?? null;


            // send and receive inline flow
            if (!string.IsNullOrWhiteSpace(HttpCallWithRetries.CalloutUrl))
            {
                if (HttpCallWithRetries.RetryDelaySeconds > 0)
                {
                    MicroflowHttpResponse = await MicroflowDurableContext.CallSubOrchestratorWithRetryAsync<MicroflowHttpResponse>(CallNames.HttpCallOrchestrator,
                                                                                                                                   HttpCallWithRetries.GetRetryOptions(),
                                                                                                                                   subInstanceId,
                                                                                                                                   (HttpCallWithRetries, runObjectResponseData));
                }
                else
                {
                    MicroflowHttpResponse = await MicroflowDurableContext.CallSubOrchestratorAsync<MicroflowHttpResponse>(CallNames.HttpCallOrchestrator,
                                                                                                                          subInstanceId,
                                                                                                                          (HttpCallWithRetries, runObjectResponseData));
                }
            }

            if (HttpCallWithRetries.EnableWebhook)
            {
                try
                {
                    if (HttpCallWithRetries.RetryDelaySeconds > 0)
                    {
                        MicroflowHttpResponse = await MicroflowDurableContext.CallSubOrchestratorWithRetryAsync<MicroflowHttpResponse>(CallNames.WebhookOrchestrator,
                                                                                                                                       HttpCallWithRetries.GetRetryOptions(),
                                                                                                                                       HttpCallWithRetries.WebhookId,
                                                                                                                                       (HttpCallWithRetries, runObjectResponseData, MicroflowHttpResponse));

                        return;
                    }

                    MicroflowHttpResponse = await MicroflowDurableContext.CallSubOrchestratorAsync<MicroflowHttpResponse>(CallNames.WebhookOrchestrator,
                                                                                                                          HttpCallWithRetries.WebhookId,
                                                                                                                          (HttpCallWithRetries, runObjectResponseData, MicroflowHttpResponse));
                }
                catch (FunctionFailedException fex)
                {
                    if (fex.InnerException is TimeoutException tex)
                    {
                        HandleWebhookTimeout(tex);

                        return;
                    }

                    throw;
                }
            }
        }

        private void HandleWebhookTimeout(TimeoutException tex)
        {
            if (!string.IsNullOrEmpty(HttpCallWithRetries.SubStepsToRunForWebhookTimeout))
            {
                MicroflowHttpResponse = new MicroflowHttpResponse()
                {
                    Success = false,
                    HttpResponseStatusCode = -408,
                    SubStepsToRun = JsonSerializer.Deserialize<List<int>>(HttpCallWithRetries.SubStepsToRunForWebhookTimeout)
                };
            }
            else
            {
                MicroflowHttpResponse = new MicroflowHttpResponse()
                {
                    Success = false,
                    HttpResponseStatusCode = -408,
                    Content = tex.Message
                };
            }
        }

        /// <summary>
        /// Get and set the HttpCallWithRetries from table storage
        /// </summary>
        private async Task GetHttpCall()
        {
            HttpCallWithRetries = await MicroflowDurableContext.CallActivityAsync<HttpCallWithRetries>(CallNames.GetStepInternal, MicroflowRun);

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
            if (MicroflowHttpResponse.Success || !HttpCallWithRetries.StopOnWebhookFailed)
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
            bool checkSubStepFromResponse = false;

            if (MicroflowHttpResponse.SubStepsToRun != null && MicroflowHttpResponse.SubStepsToRun.Count > 0)
            {
                checkSubStepFromResponse = true;
            }

            string[] stepsAndCounts = HttpCallWithRetries.SubSteps.Split(Splitter, StringSplitOptions.RemoveEmptyEntries);

            List<Task<CanExecuteResult>> canExecuteTasks = new();

            for (int i = 0; i < stepsAndCounts.Length; i += 3)
            {
                // check parentCount
                // execute immediately if parentCount is 1
                int parentCount = Convert.ToInt32(stepsAndCounts[i + 1]);
                int waitForAllParents = Convert.ToInt32(stepsAndCounts[i + 2]);

                // check if the http response had a sub step list to indicate which sub steps can execute
                if (checkSubStepFromResponse && !MicroflowHttpResponse.SubStepsToRun.Contains(Convert.ToInt32(stepsAndCounts[i])))
                {
                    continue;
                }

                if (parentCount < 2 || waitForAllParents == 0)
                {
                    // stepsAndCounts[i] is stepNumber, stepsAndCounts[i + 1] is parentCount, stepsAndCounts[i + 2] is waitForAllParents
                    MicroflowRun.RunObject = new RunObject()
                    {
                        RunId = MicroflowRun.RunObject.RunId,
                        StepNumber = stepsAndCounts[i],
                        GlobalKey = MicroflowRun.RunObject.GlobalKey,
                        MicroflowStepResponseData = HttpCallWithRetries.ForwardResponseData ? MicroflowHttpResponse : null
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
                        GlobalKey = MicroflowRun.RunObject.GlobalKey,
                        MicroflowStepResponseData = HttpCallWithRetries.ForwardResponseData ? MicroflowHttpResponse : null
                    };

                    MicroflowTasks.Add(MicroflowDurableContext.CallSubOrchestratorAsync(CallNames.ExecuteStep, MicroflowRun));
                }

                canExecuteTasks.Remove(canExecuteTask);
            }
        }

        #region Region optional: no step counts
#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
        /// <summary>
        /// Durable entity to keep an in progress count for each concurrent step in the workflow/run
        /// Used by HttpCallOrchestrator and HttpCallWithCallbackOrchestrator
        /// </summary>
        [FunctionName(MicroflowEntities.StepCount)]
        public static void StepCounter([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName)
            {
                case MicroflowEntityKeys.Add:
                    ctx.SetState(ctx.GetState<int>() + 1);
                    break;
                //case "reset":
                //    ctx.SetState(0);
                //    break;
                case MicroflowEntityKeys.Read:
                    ctx.Return(ctx.GetState<int>());
                    break;
                case MicroflowEntityKeys.Subtract:
                    ctx.SetState(ctx.GetState<int>() - 1);
                    break;
                    //case "delete":
                    //    ctx.DeleteState();
                    //    break;
            }
        }
#endif
        #endregion

        /// <summary>
        /// Log the start of the step
        /// </summary>
        [Deterministic]
        private void LogStepStart(string subInstanceId)
        {
            MicroflowTasks.Add(MicroflowDurableContext.CallActivityAsync(
                CallNames.LogStep,
                new LogStepEntity(true,
                                  MicroflowRun.WorkflowName,
                                  LogRowKey,
                                  Convert.ToInt32(HttpCallWithRetries.RowKey),
                                  MicroflowRun.OrchestratorInstanceId,
                                  MicroflowRun.RunObject.RunId,
                                  MicroflowRun.RunObject.GlobalKey,
                                  HttpCallWithRetries.CalloutUrl,
                                  subOrchestrationId: subInstanceId,
                                  webhookId: HttpCallWithRetries.WebhookId)
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
                                  null,
                                  MicroflowHttpResponse.Success,
                                  MicroflowHttpResponse.HttpResponseStatusCode,
                                  string.IsNullOrWhiteSpace(MicroflowHttpResponse.Content) ? null : MicroflowHttpResponse.Content)
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
                                  null,
                                  false,
                                  MicroflowHttpResponse.HttpResponseStatusCode,
                                  string.IsNullOrWhiteSpace(MicroflowHttpResponse.Content) ? null : MicroflowHttpResponse.Content)
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
                                      null,
                                      false,
                                      -408,
                                      string.IsNullOrWhiteSpace(HttpCallWithRetries.WebhookId)
                                        ? "callout timeout"
                                        : $"action timed out, StopOnActionFailed is {HttpCallWithRetries.StopOnWebhookFailed}")
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
                                      null,
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
