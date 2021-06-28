using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.TableLogging
{
    public static class TableLogOrchestrationActivity
    {
        [FunctionName("LogOrchestration")]
        public static async Task TableLogOrchestration([ActivityTrigger] LogOrchestrationEntity logEntity)
        {
            await logEntity.LogOrchestration();
        }
    }
}