using System.Threading.Tasks;
using Azure.Data.Tables;
using Microflow.Models;
using MicroflowModels;
using MicroflowShared;

namespace Microflow.Helpers
{
    public static class MicroflowTableHelper
    {
        #region Table operations

        public static async Task LogStep(this LogStepEntity logEntity)
        {
            TableClient tableClient = TableReferences.GetLogStepsTable();

            await tableClient.UpsertEntityAsync(logEntity);
        }

        public static async Task LogOrchestration(this LogOrchestrationEntity logEntity)
        {
            TableClient tableClient = TableReferences.GetLogOrchestrationTable();

            await tableClient.UpsertEntityAsync(logEntity);
        }

        #endregion
    }
}
