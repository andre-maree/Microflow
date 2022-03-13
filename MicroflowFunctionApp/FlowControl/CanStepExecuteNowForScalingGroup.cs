using System;
using System.Threading;
using System.Threading.Tasks;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using static Microflow.Helpers.Constants;

namespace Microflow.FlowControl
{
    public static class CanStepExecuteNowForScalingGroup
    {
        [FunctionName(CallNames.CanExecuteNowInScaleGroup)]
        public static async Task CheckMaxScaleCountForGroup([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            CanExecuteNowObject canExecuteNowObject = context.GetInput<CanExecuteNowObject>();
            EntityId countId = new EntityId(CallNames.CanExecuteNowInScaleGroupCount, canExecuteNowObject.ScaleGroupId);

            EntityId scaleGroupCountId = new EntityId(CallNames.ScaleGroupMaxConcurrentInstanceCount, canExecuteNowObject.ScaleGroupId);
            int scaleGroupMaxCount = await context.CallEntityAsync<int>(scaleGroupCountId, MicroflowControlKeys.Read);
            
            if (scaleGroupMaxCount == 0)
            {
                return;
            }

            using (await context.LockAsync(countId))
            {
                int scaleGroupInProcessCount = await context.CallEntityAsync<int>(countId, MicroflowCounterKeys.Read);
                
                if (scaleGroupInProcessCount < scaleGroupMaxCount)
                {
                    await context.CallEntityAsync(countId, MicroflowCounterKeys.Add);

                    return;
                }
            }

            // 7 days in paused state till exit
            DateTime endDate = context.CurrentUtcDateTime.AddDays(7);
            // start interval seconds
            int count = 10;
            // max interval seconds
            const int max = 60; // 1 mins

            using (CancellationTokenSource cts = new CancellationTokenSource())
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
                            int scaleGroupInProcessCount = await context.CallEntityAsync<int>(countId, MicroflowCounterKeys.Read);

                            if (scaleGroupMaxCount == 0 || scaleGroupInProcessCount < scaleGroupMaxCount)
                            {
                                await context.CallEntityAsync<int>(countId, MicroflowCounterKeys.Add);

                                return;
                            }
                        }

                        if (count % 5 == 0)
                        {
                            scaleGroupMaxCount = await context.CallEntityAsync<int>(scaleGroupCountId, MicroflowControlKeys.Read);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    //Logger.LogCritical("========================TaskCanceledException==========================");
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
        [FunctionName(CallNames.CanExecuteNowInScaleGroupCount)]
        public static void CanExecuteNowInScalingGroupCounter([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName)
            {
                case MicroflowCounterKeys.Add:
                    ctx.SetState(ctx.GetState<int>() + 1);
                    break;
                case MicroflowCounterKeys.Subtract:
                    ctx.SetState(ctx.GetState<int>() - 1);
                    break;
                case MicroflowCounterKeys.Read:
                    ctx.Return(ctx.GetState<int>());
                    break;
                    //case "delete":
                    //    ctx.DeleteState();
                    //    break;
            }
        }

        [FunctionName(CallNames.ScaleGroupMaxConcurrentInstanceCount)]
        public static void ScaleGroupMaxConcurrentInstanceCount([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName.ToLowerInvariant())
            {
                case "set":
                    ctx.SetState(ctx.GetInput<int>());
                    break;
                case MicroflowCounterKeys.Read:
                    ctx.Return(ctx.GetState<int>());
                    break;
                case "delete":
                    ctx.DeleteState();
                    break;
            }
        }
    }
}