using System;
using System.Threading.Tasks;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using static Microflow.Helpers.Constants;

namespace Microflow.FlowControl
{
    public static class CanStepExecuteNowForScalingGroup
    {
        /// <summary>
     /// Calculate if a step is ready to execute by locking and counting the completed parents - for each run and each step in the run
     /// </summary>
     /// <returns>Bool to indicate if this step request can be executed or not</returns>
        [Deterministic]
        [FunctionName("CanExecuteNowInScalingGroup")]
        public static async Task<CanExecuteResult> CanExecuteNowInScalingGroup([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            CanExecuteNowObject canExecuteNowObject = context.GetInput<CanExecuteNowObject>();
            
            try
            {
                EntityId countId = new EntityId("CanExecuteNowInScalingGroupCounter",
                                                canExecuteNowObject.RunId + canExecuteNowObject.StepNumber + "_" + canExecuteNowObject.ScaleGroupId);

                using (await context.LockAsync(countId))
                {
                    int scaleGroupInProcessCount = await context.CallEntityAsync<int>(countId, MicroflowCounterKeys.Read);

                    if (scaleGroupInProcessCount < canExecuteNowObject.ScaleGroupCount)
                    {
                        // maybe needed cleanup
                        //await context.CallEntityAsync(countId, "delete");

                        await context.CallEntityAsync<int>(countId, MicroflowCounterKeys.Add);

                        return new CanExecuteResult()
                        {
                            CanExecute = true,
                            StepNumber = canExecuteNowObject.StepNumber
                        };
                    }

                    return new CanExecuteResult()
                    {
                        CanExecute = false,
                        StepNumber = canExecuteNowObject.StepNumber
                    };
                }
            }
            catch (Exception e)
            {
                // log to table error
                LogErrorEntity errorEntity = new LogErrorEntity(canExecuteNowObject.ProjectName,
                                                                Convert.ToInt32(canExecuteNowObject.StepNumber),
                                                                e.Message,
                                                                canExecuteNowObject.RunId);

                await context.CallActivityAsync(CallNames.LogError, errorEntity);

                return new CanExecuteResult()
                {
                    CanExecute = false,
                    StepNumber = canExecuteNowObject.StepNumber
                };
            }
        }

        /// <summary>
        /// Durable entity to keep a count for each run and each step in the run
        /// </summary>
        [FunctionName("CanExecuteNowInScalingGroupCounter")]
        public static void CanExecuteNowInScalingGroupCounter([EntityTrigger] IDurableEntityContext ctx)
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
                //case "delete":
                //    ctx.DeleteState();
                //    break;
            }
        }
    }
}