using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microflow.Models;
using MicroflowModels;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

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
            CloudTable table = GetErrorsTable();
            TableOperation mergeOperation = TableOperation.InsertOrMerge(logEntity);

            await table.ExecuteAsync(mergeOperation);
        }

        public static async Task LogStep(this LogStepEntity logEntity)
        {
            CloudTable table = GetLogStepsTable();
            TableOperation mergeOperation = TableOperation.InsertOrMerge(logEntity);

            await table.ExecuteAsync(mergeOperation);
        }

        public static async Task LogOrchestration(this LogOrchestrationEntity logEntity)
        {
            CloudTable table = GetLogOrchestrationTable();
            TableOperation mergeOperation = TableOperation.InsertOrMerge(logEntity);

            await table.ExecuteAsync(mergeOperation);
        }

        // TODO: move out to api app
        public static async Task<string> GetProjectAsJson(string projectName)
        {
            List<HttpCallWithRetries> steps = await GetStepsHttpCallWithRetries(projectName);
            List<Step> outSteps = new List<Step>();

            for (int i = 1; i < steps.Count; i++)
            {
                HttpCallWithRetries step = steps[i];
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

        public static async Task<List<HttpCallWithRetries>> GetStepsHttpCallWithRetries(string projectName)
        {
            CloudTable table = GetStepsTable();

            List<HttpCallWithRetries> list = new List<HttpCallWithRetries>();

            foreach (var call in await table.ExecuteQueryAsync(new TableQuery<HttpCallWithRetries>().Where(TableQuery.GenerateFilterCondition("PartitionKey",
                                                                                QueryComparisons.Equal,
                                                                                projectName))))
            {
                list.Add(new HttpCallWithRetries(call.PartitionKey, call.RowKey, call.StepId, call.SubSteps));
            }

            return list;
        }

        //public static async Task<IList<DynamicTableEntity>> ExecuteQueryAsync(this CloudTable table,
        //       TableQuery query, CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    var items = new List<DynamicTableEntity>();
        //    TableContinuationToken token = null;
        //    do
        //    {
        //        var seg =
        //            await
        //                table.ExecuteQuerySegmentedAsync(query, token, new TableRequestOptions(), new OperationContext(),
        //                    cancellationToken);

        //        token = seg.ContinuationToken;
        //        items.AddRange(seg);


        //    } while (token != null && !cancellationToken.IsCancellationRequested
        //             && (query.TakeCount == null || items.Count < query.TakeCount.Value));


        //    return items;
        //}

        public static async Task<IList<T>> ExecuteQueryAsync<T>(this CloudTable table, TableQuery<T> query, CancellationToken ct = default(CancellationToken), Action<IList<T>> onProgress = null) where T : ITableEntity, new()
        {

            var items = new List<T>();
            TableContinuationToken token = null;

            do
            {

                TableQuerySegment<T> seg = await table.ExecuteQuerySegmentedAsync<T>(query, token);
                token = seg.ContinuationToken;
                items.AddRange(seg);
                if (onProgress != null) onProgress(items);

            } while (token != null && !ct.IsCancellationRequested);

            return items;
        }

        public static async Task<HttpCallWithRetries> GetStep(this ProjectRun projectRun)
        {
            CloudTable table = GetStepsTable();
            TableOperation retrieveOperation = TableOperation.Retrieve<HttpCallWithRetries>($"{projectRun.ProjectName}", $"{projectRun.RunObject.StepNumber}");
            TableResult result = await table.ExecuteAsync(retrieveOperation);
            HttpCallWithRetries stepEnt = result.Result as HttpCallWithRetries;

            return stepEnt;
        }

        public static async Task<List<TableEntity>> GetStepEntities(string projectName)
        {
            CloudTable table = GetStepsTable();

            TableQuery<TableEntity> query = new TableQuery<TableEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey",
                                                                                                                   QueryComparisons.Equal,
                                                                                                                   projectName));

            List<TableEntity> list = new List<TableEntity>();
            //TODO use the async version
            //foreach (TableEntity entity in await table.ExecuteQueryAsync<DynamicTableEntity>(query))
            //{
            //    list.Add(entity);
            //}

            foreach (var dyna in await table.ExecuteQueryAsync(query))
            {
                list.Add(new TableEntity(dyna.PartitionKey, dyna.RowKey));
            }

            return list;
        }

        public static async Task DeleteSteps(this ProjectRun projectRun)
        {
            try
            {
                CloudTable table = GetStepsTable();

                List<TableEntity> steps = await GetStepEntities(projectRun.ProjectName).ConfigureAwait(false);
                List<Task> batchTasks = new List<Task>();

                if (steps.Count > 0)
                {
                    TableBatchOperation batchop = new TableBatchOperation();

                    foreach (TableEntity entity in steps)
                    {
                        entity.ETag = "*";
                        TableOperation delop = TableOperation.Delete(entity);
                        batchop.Add(delop);

                        if (batchop.Count == 100)
                        {
                            batchTasks.Add(table.ExecuteBatchAsync(batchop));
                            batchop = new TableBatchOperation();
                        }
                    }

                    if (batchop.Count > 0)
                    {
                        batchTasks.Add(table.ExecuteBatchAsync(batchop));
                    }
                }

                await Task.WhenAll(batchTasks).ConfigureAwait(false);
            }
            // TODO: find out why this happens on delete but delete works
            catch (StorageException e)
            {
                if (!e.Message.Equals("Element 0 in the batch returned an unexpected response code."))
                {
                    throw e;
                }
            }
        }

        /// <summary>
        /// Called on start to create needed tables
        /// </summary>
        public static async Task CreateTables()
        {
            // StepsMyProject for step config
            CloudTable stepsTable = GetStepsTable();

            // MicroflowLog table
            CloudTable logOrchestrationTable = GetLogOrchestrationTable();

            // MicroflowLog table
            CloudTable logStepsTable = GetLogStepsTable();

            // ErrorMyProject table
            CloudTable errorsTable = GetErrorsTable();

            Task<bool> t1 = stepsTable.CreateIfNotExistsAsync();
            Task<bool> t2 = logOrchestrationTable.CreateIfNotExistsAsync();
            Task<bool> t3 = logStepsTable.CreateIfNotExistsAsync();
            Task<bool> t4 = errorsTable.CreateIfNotExistsAsync();

            await t1;
            await t2;
            await t3;
            await t4;
        }

        #endregion

        #region Get table references

        private static CloudTable GetErrorsTable()
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"MicroflowLogErrors");
        }

        public static CloudTable GetStepsTable()
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"MicroflowStepConfigs");
        }

        private static CloudTable GetLogOrchestrationTable()
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"MicroflowLogOrchestrations");
        }

        private static CloudTable GetLogStepsTable()
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"MicroflowLogSteps");
        }

        private static CloudTableClient GetTableClient()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("MicroflowStorage"));

            return storageAccount.CreateCloudTableClient();
        }

        #endregion
    }
}
