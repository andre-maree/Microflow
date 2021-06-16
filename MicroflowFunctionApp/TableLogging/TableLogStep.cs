using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Microflow.FlowControl
{
    public static class TableLogStep
    {
        [FunctionName("TableLogStep")]
        public static async Task LogToStep(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            LogStepEntity logEntity = context.GetInput<LogStepEntity>();

            await context.CallActivityAsync<LogStepEntity>("TableLogActivity", logEntity);
        }

        [FunctionName("TableLogActivity")]
        public static async Task TableLogActivity([ActivityTrigger] LogStepEntity logEntity, ILogger log)
        {
            await MicroflowTableHelper.LogStep(logEntity);
        }
    }
}