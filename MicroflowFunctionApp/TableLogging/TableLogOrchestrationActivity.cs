using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.MicroflowTableModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using static MicroflowModels.Constants.Constants;

namespace Microflow.TableLogging
{
    public static class TableLogOrchestrationActivity
    {
        [FunctionName(CallNames.LogOrchestration)]
        public static async Task TableLogOrchestration([ActivityTrigger] LogOrchestrationEntity logEntity)
        {
            await logEntity.LogOrchestration();
        }
    }
}