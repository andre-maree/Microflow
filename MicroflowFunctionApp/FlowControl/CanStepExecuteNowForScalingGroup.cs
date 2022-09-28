#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_SCALEGROUPS && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_SCALEGROUPS && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT
using System;
using System.Threading;
using System.Threading.Tasks;
using Microflow.Models;
using MicroflowModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using static MicroflowModels.Constants;

namespace Microflow.FlowControl
{
    /// <summary>
    /// Check if the step can execute taking into account the max concurrent steps in the scale group
    /// </summary>
    public static class CanStepExecuteNowForScalingGroup
    {
        [FunctionName(ScaleGroupCalls.CanExecuteNowInScaleGroup)]
        public static async Task CheckMaxScaleCountForGroup([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            CanExecuteNowObject canExecuteNowObject = context.GetInput<CanExecuteNowObject>();
            EntityId countId = new(ScaleGroupCalls.CanExecuteNowInScaleGroupCount, canExecuteNowObject.ScaleGroupId);

            EntityId scaleGroupCountId = new(ScaleGroupCalls.ScaleGroupMaxConcurrentInstanceCount, canExecuteNowObject.ScaleGroupId);
            int scaleGroupMaxCount = await context.CallEntityAsync<int>(scaleGroupCountId, MicroflowControlKeys.Read);

            if (scaleGroupMaxCount == 0)
            {
                return;
            }

            using (await context.LockAsync(countId))
            {
                int scaleGroupInProcessCount = await context.CallEntityAsync<int>(countId, MicroflowEntityKeys.Read);

                if (scaleGroupInProcessCount < scaleGroupMaxCount)
                {
                    await context.CallEntityAsync(countId, MicroflowEntityKeys.Add);

                    return;
                }
            }

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

                        using (await context.LockAsync(countId))
                        {
                            int scaleGroupInProcessCount = await context.CallEntityAsync<int>(countId, MicroflowEntityKeys.Read);

                            if (scaleGroupInProcessCount < scaleGroupMaxCount)
                            {
                                await context.CallEntityAsync<int>(countId, MicroflowEntityKeys.Add);

                                return;
                            }
                        }
                    }
                }
                catch (TaskCanceledException tex)
                {
                    // log to table error
                    LogErrorEntity errorEntity = new(canExecuteNowObject.WorkflowName,
                                                                    Convert.ToInt32(canExecuteNowObject.StepNumber),
                                                                    tex.Message,
                                                                    canExecuteNowObject.RunId);

                    await context.CallActivityAsync(CallNames.LogError, errorEntity);
                }
                catch (Exception e)
                {
                    // log to table error
                    LogErrorEntity errorEntity = new(canExecuteNowObject.WorkflowName,
                                                                    Convert.ToInt32(canExecuteNowObject.StepNumber),
                                                                    e.Message,
                                                                    canExecuteNowObject.RunId);

                    await context.CallActivityAsync(CallNames.LogError, errorEntity);
                }
                finally
                {
                    cts.Dispose();
                }
            }
        }

        /// <summary>
        /// Durable entity to keep a count for each run and each step in the run
        /// </summary>
        [FunctionName(ScaleGroupCalls.CanExecuteNowInScaleGroupCount)]
        public static void CanExecuteNowInScalingGroupCounter([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName)
            {
                case MicroflowEntityKeys.Add:
                    ctx.SetState(ctx.GetState<int>() + 1);
                    break;
                case MicroflowEntityKeys.Subtract:
                    int state = ctx.GetState<int>();
                    ctx.SetState(state <= 0 ? 0 : state - 1);
                    break;
                case MicroflowEntityKeys.Read:
                    ctx.Return(ctx.GetState<int>());
                    break;
                    //case "delete":
                    //    ctx.DeleteState();
                    //    break;
            }
        }

        [FunctionName(ScaleGroupCalls.ScaleGroupMaxConcurrentInstanceCount)]
        public static void ScaleGroupMaxConcurrentInstanceCounter([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName.ToLowerInvariant())
            {
                case MicroflowEntityKeys.Set:
                    ctx.SetState(ctx.GetInput<int>());
                    break;
                case MicroflowEntityKeys.Read:
                    ctx.Return(ctx.GetState<int>());
                    break;
                //case "delete":
                //    ctx.DeleteState();
                //    break;
            }
        }
    }
}
#endif