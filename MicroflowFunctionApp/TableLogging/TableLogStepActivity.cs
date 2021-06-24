using System.Threading.Tasks;
using Microflow.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.FlowControl
{
    public static class TableLogStepActivity
    {
        [FunctionName("LogStep")]
        public static async Task TableLogActivity([ActivityTrigger] LogStepEntity logEntity)
        {
            await MicroflowTableHelper.LogStep(logEntity);
        }
    }
}