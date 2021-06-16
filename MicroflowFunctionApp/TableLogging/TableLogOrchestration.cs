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
    public static class TableLogOrchestration
    {
        [FunctionName("TableLogOrchestration")]
        public static async Task LogToOrchestration(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            LogOrchestrationEntity logEntity = context.GetInput<LogOrchestrationEntity>();

            await context.CallActivityAsync<LogOrchestrationEntity>("TableLogOrchestrationActivity", logEntity);
        }

        [FunctionName("TableLogOrchestrationActivity")]
        public static async Task TableLogOrchestrationActivity([ActivityTrigger] LogOrchestrationEntity logEntity, ILogger log)
        {
            await MicroflowTableHelper.LogOrchestration(logEntity);
        }
    }
}