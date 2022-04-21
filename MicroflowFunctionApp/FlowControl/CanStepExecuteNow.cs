using System;
using System.Threading.Tasks;
using Microflow.Models;
using MicroflowModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using static MicroflowModels.Constants;

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
                EntityId countId = new(MicroflowEntities.CanExecuteNowCount,
                                                canExecuteNowObject.RunId + canExecuteNowObject.StepNumber);

                CanExecuteResult canExecuteResult = null;

                using (await context.LockAsync(countId))
                {
                    int parentCompletedCount = await context.CallEntityAsync<int>(countId, MicroflowEntityKeys.Read);

                    if (parentCompletedCount + 1 >= canExecuteNowObject.ParentCount)
                    {
                        // cleanup with silnalentity
                        //context.SignalEntity(countId, "delete");

                        canExecuteResult = new CanExecuteResult()
                        {
                            CanExecute = true,
                            StepNumber = canExecuteNowObject.StepNumber
                        };
                    }
                    else
                    {
                        await context.CallEntityAsync<int>(countId, MicroflowEntityKeys.Add);

                        canExecuteResult = new CanExecuteResult()
                        {
                            CanExecute = false,
                            StepNumber = canExecuteNowObject.StepNumber
                        };
                    }
                }

                if(canExecuteResult.CanExecute)
                {
                    context.SignalEntity(countId, MicroflowEntityKeys.Delete);
                }

                return canExecuteResult;
            }
            catch (Exception e)
            {
                // log to table error
                LogErrorEntity errorEntity = new(canExecuteNowObject.WorkflowName,
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
                case MicroflowEntityKeys.Add:
                    ctx.SetState(ctx.GetState<int>() + 1);
                    break;
                //case "reset":
                //    ctx.SetState(0);
                //    break;
                case MicroflowEntityKeys.Read:
                    ctx.Return(ctx.GetState<int>());
                    break;
                case MicroflowEntityKeys.Delete:
                    ctx.DeleteState();
                    break;
            }
        }
    }
}