using Azure.Data.Tables;
using MicroflowModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microflow.MicroflowTableModels;

namespace MicroflowTest
{
    internal class LogReader
    {
        public static async Task<List<LogOrchestrationEntity>> GetOrchLog(string workflowName)
        {
            List<LogOrchestrationEntity> li = new List<LogOrchestrationEntity>();
            TableClient tableClient = GetLogOrchestrationTable();

            var logTask = tableClient.QueryAsync<LogOrchestrationEntity>(filter: $"PartitionKey eq '{workflowName}'");
            
            await foreach(var log in logTask)
            {
                li.Add(log);
            }

            return li;
        }

        public static async Task<List<LogStepEntity>> GetStepsLog(string workflowName, string instanceId)
        {
            List<LogStepEntity> li = new List<LogStepEntity>();
            TableClient tableClient = GetStepsLogTable();

            var logTask = tableClient.QueryAsync<LogStepEntity>(filter: $"PartitionKey eq '{workflowName}__{instanceId}'");

            await foreach (var log in logTask)
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

        public static TableClient GetStepsLogTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogSteps");
        }

        public static TableClient GetStepsTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowStepConfigs");
        }

        public static TableClient GetLogOrchestrationTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogOrchestrations");
        }

        public static TableServiceClient GetTableClient()
        {
            return new TableServiceClient("UseDevelopmentStorage=true");
        }
    }
}
