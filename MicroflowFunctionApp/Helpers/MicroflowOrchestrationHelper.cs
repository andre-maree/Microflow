using Microflow.Models;
using MicroflowModels;
using MicroflowModels.Helpers;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static MicroflowModels.Constants.Constants;

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
            string logRowKey = TableHelpers.GetTableLogRowKeyDescendingByDate(context.CurrentUtcDateTime, $"_{workflowRun.OrchestratorInstanceId}");

            await context.LogOrchestrationStartAsync(log, workflowRun, logRowKey);

            await context.MicroflowStart(log, workflowRun);

            // log to table workflow completed
            Task logTask = context.LogOrchestrationEnd(workflowRun, logRowKey);

            context.SetMicroflowStateReady(workflowRun);

            await logTask;
            // done
            log.LogError($"Workflow run {workflowRun.WorkflowName} completed successfully...");
            log.LogError("<<<<<<<<<<<<<<<<<<<<<<<<<-----> !!! A GREAT SUCCESS  !!! <----->>>>>>>>>>>>>>>>>>>>>>>>>");
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
                Task<HttpCallWithRetries> httpTask = context.CallActivityAsync<HttpCallWithRetries>(CallNames.GetStep, workflowRun);

                string guid = context.NewGuid().ToString();

                workflowRun.RunObject.RunId = guid;

                log.LogError($"Started Run ID {workflowRun.RunObject.RunId}...");

                // pass in the current loop count so it can be used downstream/passed to the micro-services
                workflowRun.CurrentLoop = i;

                List<Task> subTasks = new List<Task>();

                HttpCallWithRetries httpCallWithRetries = await httpTask;

                string[] stepsAndCounts = httpCallWithRetries.SubSteps.Split(Splitter, StringSplitOptions.RemoveEmptyEntries);

                for (int j = 0; j < stepsAndCounts.Length; j += 2)
                {
                    workflowRun.RunObject = new RunObject()
                    {
                        RunId = guid,
                        StepNumber = stepsAndCounts[j],
                        GlobalKey = workflowRun.RunObject.GlobalKey
                    };

                    subTasks.Add(context.CallSubOrchestratorAsync(CallNames.ExecuteStep, workflowRun));
                }

                await Task.WhenAll(subTasks);

                log.LogError($"Run ID {guid} completed successfully...");
            }
        }

        /// <summary>
        /// Check if workflow ready is true, else wait with a timer (this is a durable monitor), called from start
        /// </summary>
        [Deterministic]
        public static async Task<bool> CheckAndWaitForReadyToRun(this IDurableOrchestrationContext context,
                                                                 string workflowName,
                                                                 ILogger log,
                                                                 string globalKey = null)
        {
            EntityId projStateId = new EntityId(MicroflowStateKeys.WorkflowState, workflowName);
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
                // 7 days in paused state till exit
                DateTime endDate = context.CurrentUtcDateTime.AddDays(7);
                // start interval seconds
                int count = 15;
                // max interval seconds
                const int max = 300; // 5 mins

                using (CancellationTokenSource cts = new CancellationTokenSource())
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
                    catch (TaskCanceledException)
                    {
                        log.LogCritical("========================TaskCanceledException==========================");
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
            EntityId projStateId = new EntityId(MicroflowStateKeys.WorkflowState, workflowRun.WorkflowName);

            context.SignalEntity(projStateId, MicroflowControlKeys.Ready);
        }

        [Deterministic]
        public static async Task LogOrchestrationEnd(this IDurableOrchestrationContext context,
                                                     MicroflowRun workflowRun,
                                                     string logRowKey)
        {
            var logEntity = new LogOrchestrationEntity(false,
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
                                                            ILogger log,
                                                            MicroflowRun workflowRun,
                                                            string logRowKey)
        {
            LogOrchestrationEntity logEntity = new LogOrchestrationEntity(true,
                                                                          workflowRun.WorkflowName,
                                                                          logRowKey,
                                                                          $"{workflowRun.WorkflowName} started...",
                                                                          context.CurrentUtcDateTime,
                                                                          workflowRun.OrchestratorInstanceId,
                                                                          workflowRun.RunObject.GlobalKey);

            await context.CallActivityAsync(CallNames.LogOrchestration, logEntity);

            log.LogInformation($"Started orchestration with ID = '{context.InstanceId}', workflow = '{workflowRun.WorkflowName}'");
        }
    }
}
