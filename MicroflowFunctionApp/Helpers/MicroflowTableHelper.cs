using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Microflow.Models;
using MicroflowModels;

namespace Microflow.Helpers
{
    public static class MicroflowTableHelper
    {
        #region Formatting

        public static string GetTableLogRowKeyDescendingByDate(DateTime date, string postfix)
        {
            return $"{String.Format("{0:D19}", DateTime.MaxValue.Ticks - date.Ticks)}{postfix}";
        }

        public static string GetTableRowKeyDescendingByDate()
        {
            return $"{String.Format("{0:D19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks)}{Guid.NewGuid()}";
        }

        #endregion

        #region Table operations

        public static async Task LogError(this LogErrorEntity logEntity)
        {
            TableClient tableClient = GetErrorsTable();


            await tableClient.UpsertEntityAsync(logEntity);
        }

        public static async Task LogStep(this LogStepEntity logEntity)
        {
            TableClient tableClient = GetLogStepsTable();

            await tableClient.UpsertEntityAsync(logEntity);
        }

        public static async Task LogOrchestration(this LogOrchestrationEntity logEntity)
        {
            TableClient tableClient = GetLogOrchestrationTable();

            await tableClient.UpsertEntityAsync(logEntity);
        }

        // TODO: move out to api app
        public static async Task<string> GetProjectAsJson(string projectName)
        {
            AsyncPageable<HttpCallWithRetries> steps = GetStepsHttpCallWithRetries(projectName);

            List<Step> outSteps = new List<Step>();

            await foreach (HttpCallWithRetries step in steps)
            {
                Step newstep = new Step()
                {
                    StepId = step.RowKey,
                    CallbackTimeoutSeconds = step.CallbackTimeoutSeconds,
                    CalloutTimeoutSeconds = step.CalloutTimeoutSeconds,
                    StopOnActionFailed = step.StopOnActionFailed,
                    CallbackAction = step.CallbackAction,
                    IsHttpGet = step.IsHttpGet,
                    CalloutUrl = step.CalloutUrl,
                    RetryOptions = step.RetryDelaySeconds == 0 ? null : new MicroflowRetryOptions()
                    {
                        BackoffCoefficient = step.RetryBackoffCoefficient,
                        DelaySeconds = step.RetryDelaySeconds,
                        MaxDelaySeconds = step.RetryMaxDelaySeconds,
                        MaxRetries = step.RetryMaxRetries,
                        TimeOutSeconds = step.RetryTimeoutSeconds
                    }
                };

                List<int> subStepsList = new List<int>();
                List<List<int>> stepEntList = JsonSerializer.Deserialize<List<List<int>>>(step.SubSteps);

                foreach (List<int> s in stepEntList)
                {
                    subStepsList.Add(s[0]);
                }

                newstep.SubSteps = subStepsList;

                outSteps.Add(newstep);
            }

            return JsonSerializer.Serialize(outSteps);
        }

        public static AsyncPageable<HttpCallWithRetries> GetStepsHttpCallWithRetries(string projectName)
        {
            TableClient tableClient = GetStepsTable();

            return tableClient.QueryAsync<HttpCallWithRetries>(filter: $"PartitionKey eq '{projectName}'");
        }

        public static async Task<HttpCallWithRetries> GetStep(this ProjectRun projectRun)
        {
            TableClient tableClient = GetStepsTable();

            return await tableClient.GetEntityAsync<HttpCallWithRetries>(projectRun.ProjectName, projectRun.RunObject.StepNumber);
        }

        public static AsyncPageable<TableEntity> GetStepEntities(string projectName)
        {
            TableClient tableClient = GetStepsTable();

            return tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{projectName}'", select: new List<string>() { "PartitionKey", "RowKey" });
        }

        public static async Task DeleteSteps(this ProjectRun projectRun)
        {
            TableClient tableClient = GetStepsTable();

            var steps = GetStepEntities(projectRun.ProjectName);
            List<TableTransactionAction> batch = new List<TableTransactionAction>();
            List<Task> batchTasks = new List<Task>();

            await foreach (TableEntity entity in steps)
            {
                batch.Add(new TableTransactionAction(TableTransactionActionType.Delete, entity));

                if (batch.Count == 100)
                {
                    batchTasks.Add(tableClient.SubmitTransactionAsync(batch));
                    batch = new List<TableTransactionAction>();
                }
            }

            if (batch.Count > 0)
            {
                batchTasks.Add(tableClient.SubmitTransactionAsync(batch));
            }

            await Task.WhenAll(batchTasks);
        }

        /// <summary>
        /// Called on start to create needed tables
        /// </summary>
        public static async Task CreateTables()
        {
            // StepsMyProject for step config
            TableClient stepsTable = GetStepsTable();

            // MicroflowLog table
            TableClient logOrchestrationTable = GetLogOrchestrationTable();

            // MicroflowLog table
            TableClient logStepsTable = GetLogStepsTable();

            // ErrorMyProject table
            TableClient errorsTable = GetErrorsTable();

            Task<Response<TableItem>> t1 = stepsTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t2 = logOrchestrationTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t3 = logStepsTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t4 = errorsTable.CreateIfNotExistsAsync();

            await t1;
            await t2;
            await t3;
            await t4;
        }

        #endregion

        #region Get table references

        private static TableClient GetErrorsTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogErrors");
        }

        public static TableClient GetStepsTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowStepConfigs");
        }

        private static TableClient GetLogOrchestrationTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogOrchestrations");
        }

        private static TableClient GetLogStepsTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogSteps");
        }

        private static TableServiceClient GetTableClient()
        {
            return new TableServiceClient(Environment.GetEnvironmentVariable("MicroflowStorage"));
        }

        #endregion
    }
}
