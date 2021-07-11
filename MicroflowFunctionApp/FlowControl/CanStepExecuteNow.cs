using System;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.FlowControl
{
    public static class CanStepExecuteNow
    {
        /// <summary>
        /// Calculate if a step is ready to execute by locking and counting the completed parents - for each run and each step in the run
        /// </summary>
        /// <returns>Bool to indicate if this step request can be executed or not</returns>
        [FunctionName("CanExecuteNow")]
        public static async Task<CanExecuteResult> CanExecuteNow([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            CanExecuteNowObject canExecuteNowObject = context.GetInput<CanExecuteNowObject>();
            try
            {
                EntityId countId = new EntityId(MicroflowEntities.CanExecuteNowCounter, canExecuteNowObject.RunId + canExecuteNowObject.StepNumber);

                using (await context.LockAsync(countId))
                {
                    int parentCompletedCount = await context.CallEntityAsync<int>(countId, MicroflowCounterKeys.Read);

                    if (parentCompletedCount + 1 >= canExecuteNowObject.ParentCount)
                    {
                        // maybe needed cleanup
                        //await context.CallEntityAsync(countId, "delete");

                        return new CanExecuteResult() { CanExecute = true, StepNumber = canExecuteNowObject.StepNumber };
                    }

                    await context.CallEntityAsync<int>(countId, MicroflowCounterKeys.Add);

                    return new CanExecuteResult() { CanExecute = false, StepNumber = canExecuteNowObject.StepNumber };
                }
            }
            catch (Exception e)
            {
                // log to table error
                LogErrorEntity errorEntity = new LogErrorEntity(canExecuteNowObject.ProjectName, Convert.ToInt32(canExecuteNowObject.StepNumber), e.Message, canExecuteNowObject.RunId);
                await context.CallActivityAsync("LogError", errorEntity);

                return new CanExecuteResult() { CanExecute = false, StepNumber = canExecuteNowObject.StepNumber };
            }
        }

        /// <summary>
        /// Durable entity to keep a count for each run and each step in the run
        /// </summary>
        [FunctionName(MicroflowEntities.CanExecuteNowCounter)]
        public static void CanExecuteNowCounter([EntityTrigger] IDurableEntityContext ctx)
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
                //case "delete":
                //    ctx.DeleteState();
                //    break;
            }
        }
    }
}