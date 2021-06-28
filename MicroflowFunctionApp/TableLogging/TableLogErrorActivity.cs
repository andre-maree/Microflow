using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.TableLogging
{
    public static class TableLogginActivity
    {
        [FunctionName("LogError")]
        public static async Task TableLogActivity([ActivityTrigger] LogErrorEntity logEntity)
        {
            await MicroflowTableHelper.LogError(logEntity);
        }
    }
}