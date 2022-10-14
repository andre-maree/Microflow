using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.MicroflowTableModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using static MicroflowModels.Constants;

namespace Microflow.Logging
{
    public static class TableLogWebhook
    {
        [FunctionName(CallNames.LogWebhook)]
        public static async Task TableLogWebhookActivity([ActivityTrigger] LogWebhookEntity logEntity)
        {
            await logEntity.LogWebhook();
        }
    }
}