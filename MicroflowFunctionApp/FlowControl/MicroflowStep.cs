using System;
using System.Threading.Tasks;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using static Microflow.Helpers.Constants;

namespace Microflow.FlowControl
{
    public static class MicroflowStep
    {
        /// <summary>
        /// Recursive step execution and sub-step can execute now calculations
        /// </summary>
        [Deterministic]
        [FunctionName(CallNames.ExecuteStep)]
        public static async Task ExecuteStep([OrchestrationTrigger] IDurableOrchestrationContext context,
                                             ILogger inLog)
        {
            MicroflowRun workflowRun = context.GetInput<MicroflowRun>();
            MicroflowContext microflowContext = null;

            try
            {
                microflowContext = new MicroflowContext(context, workflowRun, inLog);

                await microflowContext.RunMicroflow();
            }
            catch (Exception e)
            {
                if (microflowContext != null)
                {
                    string stepNumber = microflowContext.HttpCallWithRetries == null 
                        ? "-2" 
                        : microflowContext.HttpCallWithRetries.RowKey;

                    // log to table workflow completed
                    LogErrorEntity errorEntity = new LogErrorEntity(workflowRun?.WorkflowName,
                                                                    Convert.ToInt32(stepNumber),
                                                                    e.Message,
                                                                    workflowRun?.RunObject?.RunId);

                    await context.CallActivityAsync(CallNames.LogError, errorEntity);

                    throw;
                }
            }
        }
    }
}
