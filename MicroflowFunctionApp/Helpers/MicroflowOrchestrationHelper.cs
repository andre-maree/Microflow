using Microflow.MicroflowTableModels;
using MicroflowModels;
using MicroflowModels.Helpers;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static MicroflowModels.Constants;

namespace Microflow.Helpers
{
    public static class MicroflowOrchestrationHelper
    {
        /// <summary>
        /// Start a new workflow run
        /// </summary>
        /// <returns></returns>
        [Deterministic]
        public static async Task StartMicroflow(this IDurableOrchestrationContext context,
                                                       ILogger log,
                                                       MicroflowRun workflowRun)
        {
            // log start
            string logRowKey = TableHelper.GetTableLogRowKeyDescendingByDate(context.CurrentUtcDateTime, $"_{workflowRun.OrchestratorInstanceId}");

            await context.LogOrchestrationStartAsync(workflowRun, logRowKey);

            log.LogInformation($"Started orchestration with ID = '{context.InstanceId}', workflow = '{workflowRun.WorkflowName}'");

            await context.MicroflowStart(log, workflowRun);

            // log to table workflow completed
            Task logTask = context.LogOrchestrationEnd(workflowRun, logRowKey);

            context.SetMicroflowStateReady(workflowRun);

            await logTask;
            // done
            log.LogWarning($"Workflow run {workflowRun.WorkflowName} completed successfully...");
            log.LogWarning("<<<<<<<<<<<<<<<<<<<<<<<<<-----> !!! A GREAT SUCCESS  !!! <----->>>>>>>>>>>>>>>>>>>>>>>>>");
        }

        /// <summary>
        /// This is called from on start of workflow execution,
        /// does the looping and calls "ExecuteStep" for each top level step,
        /// by getting step -1 from table storage
        /// </summary>
        [Deterministic]
        public static async Task MicroflowStart(this IDurableOrchestrationContext context,
                                                          ILogger log,
                                                          MicroflowRun workflowRun)
        {
            // do the looping
            for (int i = 1; i <= workflowRun.Loop; i++)
            {
                // get the top container step from table storage (from PrepareWorkflow)
                workflowRun.RunObject.StepNumber = "-1";
                Task<HttpCallWithRetries> httpTask = context.CallActivityAsync<HttpCallWithRetries>(CallNames.GetStepInternal, workflowRun);

                string guid = context.NewGuid().ToString();

                workflowRun.RunObject.RunId = guid;

                log.LogCritical($"Started Run ID {workflowRun.RunObject.RunId}...");

                // pass in the current loop count so it can be used downstream/passed to the micro-services
                workflowRun.CurrentLoop = i;

                List<Task> subTasks = new();

                HttpCallWithRetries httpCallWithRetries = await httpTask;

                string[] stepsAndCounts = httpCallWithRetries.SubSteps.Split(Splitter, StringSplitOptions.RemoveEmptyEntries);

                for (int j = 0; j < stepsAndCounts.Length; j += 3)
                {
                    workflowRun.RunObject = new RunObject()
                    {
                        RunId = guid,
                        StepNumber = stepsAndCounts[j],
                        GlobalKey = workflowRun.RunObject.GlobalKey,
                        MicroflowStepResponseData = workflowRun.RunObject.MicroflowStepResponseData
                    };

                    subTasks.Add(context.CallSubOrchestratorAsync(CallNames.ExecuteStep, workflowRun));
                }

                await Task.WhenAll(subTasks);

                log.LogCritical($"Run ID {guid} completed successfully...");
            }
        }

        /// <summary>
        /// Check if workflow ready is true, else wait with a timer (this is a durable monitor), called from start
        /// </summary>
        [Deterministic]
        public static async Task<bool> MicroflowCheckAndWaitForReadyToRun(this IDurableOrchestrationContext context,
                                                                 string workflowName,
                                                                 string globalKey = null)
        {
            EntityId projStateId = new(MicroflowStateKeys.WorkflowState, workflowName);
            Task<int> projStateTask = context.CallEntityAsync<int>(projStateId, MicroflowControlKeys.Read);

            bool doGlobal = !string.IsNullOrWhiteSpace(globalKey);
            Task<int> globalSateTask = null;
            EntityId globalStateId;
            int globalState = 0;
            if (doGlobal)
            {
                globalStateId = new EntityId(MicroflowStateKeys.GlobalState, globalKey);
                globalSateTask = context.CallEntityAsync<int>(globalStateId, MicroflowControlKeys.Read);
            }

            int projState = await projStateTask;

            if (doGlobal)
            {
                globalState = await globalSateTask;
            }

            // check workflow and global states, run step if both states are ready
            if (projState == MicroflowStates.Ready && globalState == MicroflowStates.Ready)
            {
                return true;
            }
            // if workflow or global key state is paused, then pause this step, and wait and poll states by timer
            else if (projState == MicroflowStates.Paused || globalState == MicroflowStates.Paused)
            {
                DateTime endDate = context.CurrentUtcDateTime.AddHours(PollingConfig.PollingMaxHours);
                // start interval seconds
                int count = PollingConfig.PollingIntervalSeconds;
                // max interval seconds
                int max = PollingConfig.PollingIntervalMaxSeconds;

                using (CancellationTokenSource cts = new())
                {
                    try
                    {
                        while (context.CurrentUtcDateTime < endDate)
                        {
                            DateTime deadline = context.CurrentUtcDateTime.Add(TimeSpan.FromSeconds(count < max ? count : max));
                            await context.CreateTimer(deadline, cts.Token);
                            count++;

                            // timer wait completed, refresh pause states
                            projStateTask = context.CallEntityAsync<int>(projStateId, MicroflowControlKeys.Read);

                            if (doGlobal) globalSateTask = context.CallEntityAsync<int>(globalStateId, MicroflowControlKeys.Read);

                            projState = await projStateTask;

                            if (doGlobal) globalState = await globalSateTask;

                            // check pause states, exit while if not paused
                            if (projState != MicroflowStates.Paused && globalState != MicroflowStates.Paused)
                            {
                                break;
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                        LogErrorEntity errorEntity = new(workflowName, -1,
                                                                "MicroflowCheckAndWaitForReadyToRun timed out before it could find a ready state",
                                                                globalKey);

                        await context.CallActivityAsync(CallNames.LogError, errorEntity);
                    }
                    catch (Exception e)
                    {
                        LogErrorEntity errorEntity = new(workflowName, -1,
                                                                "MicroflowCheckAndWaitForReadyToRun error: " + e.Message,
                                                                globalKey);

                        await context.CallActivityAsync(CallNames.LogError, errorEntity);
                    }
                    finally
                    {
                        cts.Dispose();
                    }
                }

                // if workflow and global key state is ready, then continue to run step
                if (projState == MicroflowStates.Ready && globalState == MicroflowStates.Ready)
                {
                    return true;
                }
            }

            return false;
        }

        [Deterministic]
        public static void SetMicroflowStateReady(this IDurableOrchestrationContext context, MicroflowRun workflowRun)
        {
            EntityId projStateId = new(MicroflowStateKeys.WorkflowState, workflowRun.WorkflowName);

            context.SignalEntity(projStateId, MicroflowControlKeys.Ready);
        }

        [Deterministic]
        public static async Task LogOrchestrationEnd(this IDurableOrchestrationContext context,
                                                     MicroflowRun workflowRun,
                                                     string logRowKey)
        {
            LogOrchestrationEntity logEntity = new LogOrchestrationEntity(false,
                                                       workflowRun.WorkflowName,
                                                       logRowKey,
                                                       $"VM: {Environment.MachineName} - {workflowRun.WorkflowName} completed successfully",
                                                       context.CurrentUtcDateTime,
                                                       workflowRun.OrchestratorInstanceId,
                                                       workflowRun.RunObject.GlobalKey);

            await context.CallActivityAsync(CallNames.LogOrchestration, logEntity);
        }

        [Deterministic]
        public static async Task LogOrchestrationStartAsync(this IDurableOrchestrationContext context,
                                                            MicroflowRun workflowRun,
                                                            string logRowKey)
        {
            LogOrchestrationEntity logEntity = new(true,
                                                                          workflowRun.WorkflowName,
                                                                          logRowKey,
                                                                          $"{workflowRun.WorkflowName} started...",
                                                                          context.CurrentUtcDateTime,
                                                                          workflowRun.OrchestratorInstanceId,
                                                                          workflowRun.RunObject.GlobalKey);

            await context.CallActivityAsync(CallNames.LogOrchestration, logEntity);
        }
    }
}
