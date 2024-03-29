using System.Threading.Tasks;
using MicroflowModels;
using MicroflowModels.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using static MicroflowModels.Constants;

namespace Microflow.Logging
{
    public static class TableLogginActivity
    {
        [FunctionName(CallNames.LogError)]
        public static async Task LogError([ActivityTrigger] LogErrorEntity logEntity)
        {
            await TableHelper.LogError(logEntity);
        }
    }
}