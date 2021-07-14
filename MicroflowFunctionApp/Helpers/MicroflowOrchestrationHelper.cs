using Microflow.Models;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Microflow.Helpers.Constants;

namespace Microflow.Helpers
{
    public static class MicroflowOrchestrationHelper
    { 
        /// <summary>
      /// Start a new project run
      /// </summary>
      /// <returns></returns>
        [Deterministic]
        public static async Task StartMicroflowProject(this IDurableOrchestrationContext context, ILogger log, ProjectRun projectRun)
        {
            // log start
            string logRowKey = MicroflowTableHelper.GetTableLogRowKeyDescendingByDate(context.CurrentUtcDateTime, $"_{projectRun.OrchestratorInstanceId}");

            await context.LogOrchestrationStartAsync(log, projectRun, logRowKey);

            await context.MicroflowStartProjectRun(log, projectRun);

            // log to table workflow completed
            Task logTask = context.LogOrchestrationEnd(projectRun, logRowKey);

            context.SetProjectStateReady(projectRun);

            await logTask;
            // done
            log.LogError($"Project run {projectRun.ProjectName} completed successfully...");
            log.LogError("<<<<<<<<<<<<<<<<<<<<<<<<<-----> !!! A GREAT SUCCESS  !!! <----->>>>>>>>>>>>>>>>>>>>>>>>>");
        }

        /// <summary>
        /// This is called from on start of workflow execution,
        /// does the looping and calls "ExecuteStep" for each top level step,
        /// by getting step -1 from table storage
        /// </summary>
        [Deterministic]
        public static async Task MicroflowStartProjectRun(this IDurableOrchestrationContext context, ILogger log, ProjectRun projectRun)
        {
            // do the looping
            for (int i = 1; i <= projectRun.Loop; i++)
            {
                // get the top container step from table storage (from PrepareWorkflow)
                Task<HttpCallWithRetries> httpTask = context.CallActivityAsync<HttpCallWithRetries>(CallNames.GetStep, projectRun);

                string guid = context.NewGuid().ToString();

                projectRun.RunObject.RunId = guid;

                log.LogError($"Started Run ID {projectRun.RunObject.RunId}...");

                // pass in the current loop count so it can be used downstream/passed to the micro-services
                projectRun.CurrentLoop = i;

                List<Task> subTasks = new List<Task>();

                HttpCallWithRetries httpCallWithRetries = await httpTask;

                string[] stepsAndCounts = httpCallWithRetries.SubSteps.Split(Splitter, StringSplitOptions.RemoveEmptyEntries);

                for (int j = 0; j < stepsAndCounts.Length; j += 2)
                {
                    projectRun.RunObject = new RunObject()
                    {
                        RunId = guid,
                        StepNumber = stepsAndCounts[j],
                        GlobalKey = projectRun.RunObject.GlobalKey
                    };

                    subTasks.Add(context.CallSubOrchestratorAsync(CallNames.ExecuteStep, projectRun));
                }

                await Task.WhenAll(subTasks);

                log.LogError($"Run ID {guid} completed successfully...");
            }
        }

        /// <summary>
        /// Check if project ready is true, else wait with a timer (this is a durable monitor), called from start
        /// </summary>
        [Deterministic]
        public static async Task<bool> CheckAndWaitForReadyToRun(this IDurableOrchestrationContext context, string projectName, ILogger log, string globalKey = null)
        {
            EntityId runStateId = new EntityId(MicroflowStateKeys.ProjectStateId, projectName);

            Task<int> projStateTask = context.CallEntityAsync<int>(runStateId, MicroflowControlKeys.Read);
            Task<EntityStateResponse<int>> globStateTask = null;

            if (!string.IsNullOrWhiteSpace(globalKey))
            {
                EntityId globalStateId = new EntityId(MicroflowStateKeys.GlobalStateId, globalKey);
                globStateTask = context.CallEntityAsync<EntityStateResponse<int>>(globalStateId, MicroflowControlKeys.Read);
            }

            int projState = await projStateTask;
            int globState = MicroflowStates.Ready;

            if (globStateTask != null)
            {
                EntityStateResponse<int> globStateRes = await globStateTask;
                globState = globStateRes.EntityState;
            }

            if (projState != MicroflowStates.Ready || globState != MicroflowStates.Ready)
            {
                return false;
            }

            return true;

            //DateTime endDate = context.CurrentUtcDateTime.AddMinutes(30);
            //int count = 5;
            //int max = 20;

            //using (CancellationTokenSource cts = new CancellationTokenSource())
            //{
            //    try
            //    {
            //        while (context.CurrentUtcDateTime < endDate)
            //        {
            //            DateTime deadline = context.CurrentUtcDateTime.Add(TimeSpan.FromSeconds(count < max ? count : max));
            //            await context.CreateTimer(deadline, cts.Token);
            //            count++;

            //            if (await context.CallEntityAsync<int>(runState, "get") == 0)
            //            {
            //                break;
            //            }
            //        }
            //    }
            //    catch (TaskCanceledException)
            //    {
            //        log.LogCritical("========================TaskCanceledException==========================");
            //    }
            //    finally
            //    {
            //        cts.Dispose();
            //    }
            //}
        }

        [Deterministic]
        public static void SetProjectStateReady(this IDurableOrchestrationContext context, ProjectRun projectRun)
        {
            EntityId projStateId = new EntityId(MicroflowStateKeys.ProjectStateId, projectRun.ProjectName);

            context.SignalEntity(projStateId, MicroflowControlKeys.Ready);
        }

        [Deterministic]
        public static async Task LogOrchestrationEnd(this IDurableOrchestrationContext context, ProjectRun projectRun, string logRowKey)
        {
            var logEntity = new LogOrchestrationEntity(false,
                                                                   projectRun.ProjectName,
                                                                   logRowKey,
                                                                   $"{projectRun.ProjectName} completed successfully",
                                                                   context.CurrentUtcDateTime,
                                                                   projectRun.OrchestratorInstanceId,
                                                                   projectRun.RunObject.GlobalKey);

            await context.CallActivityAsync(CallNames.LogOrchestration, logEntity);
        }

        [Deterministic]
        public static async Task LogOrchestrationStartAsync(this IDurableOrchestrationContext context, ILogger log, ProjectRun projectRun, string logRowKey)
        {
            LogOrchestrationEntity logEntity = new LogOrchestrationEntity(true,
                                                                       projectRun.ProjectName,
                                                                       logRowKey,
                                                                       $"{projectRun.ProjectName} started...",
                                                                       context.CurrentUtcDateTime,
                                                                       projectRun.OrchestratorInstanceId,
                                                                       projectRun.RunObject.GlobalKey);

            await context.CallActivityAsync(CallNames.LogOrchestration, logEntity);

            log.LogInformation($"Started orchestration with ID = '{context.InstanceId}', Project = '{projectRun.ProjectName}'");
        }
    }
}
