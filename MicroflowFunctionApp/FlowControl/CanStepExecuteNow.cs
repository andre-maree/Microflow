using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow
{
    public static class CanStepExecuteNow
    {
        /// <summary>
        /// Calculate if a step is ready to execute by locking and counting the completed parents - for each run and each step in the run
        /// </summary>
        /// <returns>Bool to indicate if this step request can be executed or not</returns>
        [FunctionName("CanExecuteNow")]
        public static async Task<bool> CanExecuteNow([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            CanExecuteNowObject canExecuteNowObject = context.GetInput<CanExecuteNowObject>();
            EntityId countId = new EntityId(nameof(Counter), canExecuteNowObject.RunId + canExecuteNowObject.StepId);

            using (await context.LockAsync(countId))
            {
                int parentCompletedCount = await context.CallEntityAsync<int>(countId, "get");

                if (parentCompletedCount + 1 >= canExecuteNowObject.ParentCount)
                {
                    // maybe needed
                    //await context.CallEntityAsync<int>(countId, "add");
                    //await context.CallEntityAsync(countId, "delete");

                    return true;
                }

                await context.CallEntityAsync<int>(countId, "add");

                return false;
            }
        }

        /// <summary>
        /// Durable entity to keep a count for each run and each step in the run
        /// </summary>
        [FunctionName("Counter")]
        public static void Counter([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName.ToLowerInvariant())
            {
                case "add":
                    ctx.SetState(ctx.GetState<int>() + 1);
                    break;
                //case "reset":
                //    ctx.SetState(0);
                //    break;
                case "get":
                    ctx.Return(ctx.GetState<int>());
                    break;
                //case "delete":
                //    ctx.DeleteState();
                //    break;
            }
        }
    }
}
