using Azure.Data.Tables;
using MicroflowModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroflowTest
{
    internal class LogReader
    {
        public static async Task<List<LogErrorEntity>> GetOrchLog(string workflowName)
        {
            List<LogErrorEntity> li = new List<LogErrorEntity>();
            TableClient tableClient = GetStepsTable();

            var logTask = tableClient.QueryAsync<LogErrorEntity>(filter: $"PartitionKey eq '{workflowName}'", select: new List<string>() { "PartitionKey", "RowKey" });

            await foreach(var log in logTask)
            {
                li.Add(log);
            }

            return li;
        }

        public static TableClient GetErrorsTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogErrors");
        }

        public static TableClient GetStepsTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowStepConfigs");
        }

        public static TableServiceClient GetTableClient()
        {
            return new TableServiceClient("UseDevelopmentStorage=true");
        }
    }
}
