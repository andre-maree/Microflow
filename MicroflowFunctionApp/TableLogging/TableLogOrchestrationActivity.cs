using System.Threading.Tasks;
using Microflow.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.FlowControl
{
    public static class TableLogOrchestrationActivity
    {
        [FunctionName("LogOrchestration")]
        public static async Task TableLogOrchestration([ActivityTrigger] LogOrchestrationEntity logEntity)
        {
            await MicroflowTableHelper.LogOrchestration(logEntity);
        }
    }
}