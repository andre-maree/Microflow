using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microflow.MicroflowTableModels;
using System.Collections.Generic;
using MicroflowModels.Helpers;

namespace Microflow.Helpers
{
    public static class MicroflowTableHelper
    {
        #region Table operations

        public static async Task LogStep(this LogStepEntity logEntity)
        {
            TableClient tableClient = TableHelper.GetLogStepsTable();

            await tableClient.UpsertEntityAsync(logEntity);
        }

        public static async Task LogOrchestration(this LogOrchestrationEntity logEntity)
        {
            TableClient tableClient = TableHelper.GetLogOrchestrationTable();

            await tableClient.UpsertEntityAsync(logEntity);
        }

        public static async Task LogWebhook(this LogWebhookEntity logEntity)
        {
            TableClient tableClient = TableHelper.GetLogWebhookTable();

            await tableClient.UpsertEntityAsync(logEntity);
        }

        public static async Task<List<LogWebhookEntity>> GetWebhooks(string workflowName, string webhookId, int stepNumber, string instanceGuid = "")
        {
            TableClient tableClient = TableHelper.GetLogWebhookTable();

            string query = $"(PartitionKey eq '{workflowName}' or PartitionKey >= '{workflowName}~' and PartitionKey < '{workflowName}~~') and RowKey lt '{stepNumber + 1}' and RowKey gt '{stepNumber}~";

            if (!string.IsNullOrEmpty(instanceGuid))
            {
                query += $"{instanceGuid}~' and RowKey lt '{stepNumber}~{instanceGuid}~~'";
            }
            else
            {
                query += "'";
            }
            //"(PartitionKey eq 'Myflow_ClientX2@2.1' or PartitionKey >= 'Myflow_ClientX2@2.1~' and PartitionKey lt 'Myflow_ClientX2@2.1~~') and RowKey < '3' and RowKey gt '2~d17f312f-8b2f-5d34-aa4e-92c76236029d~'and RowKey < '3' and RowKey lt '2~d17f312f-8b2f-5d34-aa4e-92c76236029d~~'";

            AsyncPageable<LogWebhookEntity> queryResultsFilter = tableClient.QueryAsync<LogWebhookEntity>(filter: query);

            List<LogWebhookEntity> list = new();

            // Iterate the <see cref="Pageable"> to access all queried entities.
            await foreach (LogWebhookEntity qEntity in queryResultsFilter)
            {
                list.Add(qEntity);
            }

            return list;
        }

        #endregion
    }
}
