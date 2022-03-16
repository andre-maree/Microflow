using System;
using System.Threading.Tasks;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using static Microflow.Helpers.Constants;

namespace Microflow.FlowControl
{
    public static class CanStepExecuteNow
    {
        /// <summary>
        /// Calculate if a step is ready to execute by locking and counting the completed parents - for each run and each step in the run
        /// </summary>
        /// <returns>Bool to indicate if this step request can be executed or not</returns>
        [Deterministic]
        [FunctionName(CallNames.CanExecuteNow)]
        public static async Task<CanExecuteResult> CanExecuteNow([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            CanExecuteNowObject canExecuteNowObject = context.GetInput<CanExecuteNowObject>();
            try
            {
                EntityId countId = new EntityId(MicroflowEntities.CanExecuteNowCount,
                                                canExecuteNowObject.RunId + canExecuteNowObject.StepNumber);

                using (await context.LockAsync(countId))
                {
                    int parentCompletedCount = await context.CallEntityAsync<int>(countId, MicroflowCounterKeys.Read);

                    if (parentCompletedCount + 1 >= canExecuteNowObject.ParentCount)
                    {
                        // maybe needed cleanup
                        //await context.CallEntityAsync(countId, "delete");

                        return new CanExecuteResult() 
                        { 
                            CanExecute = true, 
                            StepNumber = canExecuteNowObject.StepNumber 
                        };
                    }

                    await context.CallEntityAsync<int>(countId, MicroflowCounterKeys.Add);

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
                LogErrorEntity errorEntity = new LogErrorEntity(canExecuteNowObject.WorkflowName,
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
        [Deterministic]
        [FunctionName(MicroflowEntities.CanExecuteNowCount)]
        public static void CanExecuteNowCounter([EntityTrigger] IDurableEntityContext ctx)
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