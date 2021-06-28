using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.TableLogging
{
    public static class TableLogStepActivity
    {
        [FunctionName("LogStep")]
        public static async Task TableLogActivity([ActivityTrigger] LogStepEntity logEntity)
        {
            await logEntity.LogStep();
        }
    }
}