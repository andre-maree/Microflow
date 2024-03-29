using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.MicroflowTableModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using static MicroflowModels.Constants;

namespace Microflow.Logging
{
    public static class BlobLogRequest
    {
        [FunctionName(CallNames.LogStep)]
        public static async Task TableLogActivity([ActivityTrigger] LogStepEntity logEntity)
        {
            await logEntity.LogStep();
        }
    }
}