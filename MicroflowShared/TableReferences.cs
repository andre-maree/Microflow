using Azure.Data.Tables;
using MicroflowModels.Helpers;

namespace MicroflowShared
{
    public static class TableReferences
    {
        #region Get table references

        public static TableClient GetLogOrchestrationTable()
        {
            TableServiceClient tableClient = TableHelper.GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogOrchestrations");
        }

        public static TableClient GetLogStepsTable()
        {
            TableServiceClient tableClient = TableHelper.GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogSteps");
        }

        public static TableClient GetLogWebhookTable()
        {
            TableServiceClient tableClient = TableHelper.GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogWebhooks");
        }

        #endregion
    }
}
