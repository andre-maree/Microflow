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

        //public static string GetTableLogRowKeyDescendingByDate(DateTime date, string postfix)
        //{
        //    return $"{String.Format("{0:D19}", DateTime.MaxValue.Ticks - date.Ticks)}{postfix}";
        //}

        //public static string GetTableRowKeyDescendingByDate()
        //{
        //    return $"{String.Format("{0:D19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks)}{Guid.NewGuid()}";
        //}

        #endregion

        #region Table operations

        //public static async Task LogError(this LogErrorEntity logEntity)
        //{
        //    TableClient tableClient = GetErrorsTable();


        //    await tableClient.UpsertEntityAsync(logEntity);
        //}

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
        //public static async Task<string> GetWorkflowAsJson(string workflowName)
        //{
        //    AsyncPageable<HttpCallWithRetries> steps = GetStepsHttpCallWithRetries(workflowName);

        //    List<Step> outSteps = new List<Step>();
        //    bool skip1st = true;

        //    await foreach (HttpCallWithRetries step in steps)
        //    {
        //        if (skip1st)
        //        {
        //            skip1st = false;
        //        }
        //        else
        //        {
        //            Step newstep = new Step()
        //            {
        //                StepId = step.RowKey,
        //                CallbackTimeoutSeconds = step.CallbackTimeoutSeconds,
        //                CalloutTimeoutSeconds = step.CalloutTimeoutSeconds,
        //                StopOnActionFailed = step.StopOnActionFailed,
        //                CallbackAction = step.CallbackAction,
        //                IsHttpGet = step.IsHttpGet,
        //                CalloutUrl = step.CalloutUrl,
        //                AsynchronousPollingEnabled = step.AsynchronousPollingEnabled,
        //                ScaleGroupId = step.ScaleGroupId,
        //                StepNumber = Convert.ToInt32(step.RowKey),
        //                RetryOptions = step.RetryDelaySeconds == 0 ? null : new MicroflowRetryOptions()
        //                {
        //                    BackoffCoefficient = step.RetryBackoffCoefficient,
        //                    DelaySeconds = step.RetryDelaySeconds,
        //                    MaxDelaySeconds = step.RetryMaxDelaySeconds,
        //                    MaxRetries = step.RetryMaxRetries,
        //                    TimeOutSeconds = step.RetryTimeoutSeconds
        //                }
        //            };

        //            List<int> subStepsList = new List<int>();
        //            ;
        //            string[] stepsAndCounts = step.SubSteps.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        //            for (int i = 0; i < stepsAndCounts.Length; i = i + 2)
        //            {
        //                subStepsList.Add(Convert.ToInt32(stepsAndCounts[i]));
        //            }

        //            newstep.SubSteps = subStepsList;

        //            outSteps.Add(newstep);
        //        }
        //    }

        //    TableClient wfConfigsTable = GetWorkflowConfigsTable();

        //    MicroflowConfigEntity projConfig = await wfConfigsTable.GetEntityAsync<MicroflowConfigEntity>(workflowName, "0");

        //    MicroflowModels.Microflow proj = JsonSerializer.Deserialize<MicroflowModels.Microflow>(projConfig.Config);
        //    proj.WorkflowName = workflowName;
        //    proj.Steps = outSteps;

        //    return JsonSerializer.Serialize(proj);
        //}

        //public static AsyncPageable<HttpCallWithRetries> GetStepsHttpCallWithRetries(string workflowName)
        //{
        //    TableClient tableClient = GetStepsTable();

        //    return tableClient.QueryAsync<HttpCallWithRetries>(filter: $"PartitionKey eq '{workflowName}'");
        //}

        //public static async Task<HttpCallWithRetries> GetStep(this MicroflowRun workflowRun)
        //{
        //    TableClient tableClient = GetStepsTable();

        //    return await tableClient.GetEntityAsync<HttpCallWithRetries>(workflowRun.WorkflowName, workflowRun.RunObject.StepNumber);
        //}

        public static AsyncPageable<TableEntity> GetStepEntities(string workflowName)
        {
            TableClient tableClient = GetStepsTable();

            return tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{workflowName}'", select: new List<string>() { "PartitionKey", "RowKey" });
        }

        public static async Task DeleteSteps(this MicroflowRun workflowRun)
        {
            TableClient tableClient = GetStepsTable();

            var steps = GetStepEntities(workflowRun.WorkflowName);
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
        /// Called on start to save additional workflow config not looked up during execution
        /// </summary>
        //public static async Task UpsertWorkflowConfigString(string workflowName, string workflowConfigJson)
        //{
        //    TableClient projTable = GetWorkflowConfigsTable();

        //    MicroflowConfigEntity proj = new MicroflowConfigEntity(workflowName, workflowConfigJson);

        //    await projTable.UpsertEntityAsync(proj);
        //}

        /// <summary>
        /// Called on start to create needed tables
        /// </summary>
        //public static async Task CreateTables()
        //{
        //    // StepsMyworkflow for step config
        //    TableClient stepsTable = GetStepsTable();

        //    // MicroflowLog table
        //    TableClient logOrchestrationTable = GetLogOrchestrationTable();

        //    // MicroflowLog table
        //    TableClient logStepsTable = GetLogStepsTable();

        //    // Error table
        //    TableClient errorsTable = GetErrorsTable();

        //    // workflow table
        //    TableClient workflowConfigsTable = GetWorkflowConfigsTable();

        //    Task<Response<TableItem>> t1 = stepsTable.CreateIfNotExistsAsync();
        //    Task<Response<TableItem>> t2 = logOrchestrationTable.CreateIfNotExistsAsync();
        //    Task<Response<TableItem>> t3 = logStepsTable.CreateIfNotExistsAsync();
        //    Task<Response<TableItem>> t4 = errorsTable.CreateIfNotExistsAsync();
        //    Task<Response<TableItem>> t5 = workflowConfigsTable.CreateIfNotExistsAsync();

        //    await t1;
        //    await t2;
        //    await t3;
        //    await t4;
        //    await t5;
        //}

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

        private static TableClient GetWorkflowConfigsTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowWorkflowConfigs");
        }

        private static TableServiceClient GetTableClient()
        {
            return new TableServiceClient(Environment.GetEnvironmentVariable("MicroflowStorage"));
        }

        #endregion
    }
}
