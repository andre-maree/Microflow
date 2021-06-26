using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dynamitey.DynamicObjects;
using Microsoft.Azure.Cosmos.Table;

namespace Microflow.Helpers
{
    public static class MicroflowTableHelper
    {
        public static string GetTableLogRowKeyDescendingByDate(DateTime date, string postfix)
        {
            return $"{String.Format("{0:D19}", DateTime.MaxValue.Ticks - date.Ticks)}{postfix}";
        }

        public static string GetTableRowKeyDescendingByDate()
        {
            return $"{String.Format("{0:D19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks)}{Guid.NewGuid()}";
        }
        
        public static async Task LogError(LogErrorEntity logEntity)
        {
            CloudTable table = GetErrorsTable();
            TableOperation mergeOperation = TableOperation.InsertOrMerge(logEntity);

            await table.ExecuteAsync(mergeOperation);
        }

        public static async Task LogStep(LogStepEntity logEntity)
        {
            CloudTable table = GetLogStepsTable();
            TableOperation mergeOperation = TableOperation.InsertOrMerge(logEntity);

            await table.ExecuteAsync(mergeOperation);
        }

        public static async Task LogOrchestration(LogOrchestrationEntity logEntity)
        {
            CloudTable table = GetLogOrchestrationTable();
            TableOperation mergeOperation = TableOperation.InsertOrMerge(logEntity);

            await table.ExecuteAsync(mergeOperation);
        }


        public static async Task Pause(ProjectControlEntity projectControlEntity)
        {
            CloudTable table = GetProjectControlTable();
            TableOperation mergeOperation = TableOperation.Merge(projectControlEntity);

            await table.ExecuteAsync(mergeOperation);
        }

        public static async Task<ProjectControlEntity> GetProjectControl(string projectId)
        {
            CloudTable table = GetProjectControlTable();
            TableOperation mergeOperation = TableOperation.Retrieve<ProjectControlEntity>(projectId, "0");
            TableResult result = await table.ExecuteAsync(mergeOperation);
            ProjectControlEntity projectControlEntity = result.Result as ProjectControlEntity;

            return projectControlEntity;
        }

        public static async Task<int> GetState(string projectId)
        {
            CloudTable table = GetProjectControlTable();
            TableOperation mergeOperation = TableOperation.Retrieve<ProjectControlEntity>(projectId, "0");
            TableResult result = await table.ExecuteAsync(mergeOperation);
            ProjectControlEntity projectControlEntity = result.Result as ProjectControlEntity;

            // ReSharper disable once PossibleNullReferenceException
            return projectControlEntity.State;
        }

        public static async Task<HttpCallWithRetries> GetStep(ProjectRun projectRun)
        {
            CloudTable table = GetStepsTable(projectRun.ProjectName);
            TableOperation retrieveOperation = TableOperation.Retrieve<HttpCallWithRetries>($"{projectRun.ProjectName}", $"{projectRun.RunObject.StepId}");
            TableResult result = await table.ExecuteAsync(retrieveOperation);
            HttpCallWithRetries stepEnt = result.Result as HttpCallWithRetries;

            return stepEnt;
        }

        public static async Task<List<TableEntity>> GetStepEntities(ProjectRun projectRun)
        {
            CloudTable table = GetStepsTable(projectRun.ProjectName);

            TableQuery<TableEntity> query = new TableQuery<TableEntity>();

            List<TableEntity> list = new List<TableEntity>();
            //TODO use the async version
            foreach (TableEntity entity in table.ExecuteQuery(query))
            {
                list.Add(entity);
            }

            return list;
        }

        public static async Task DeleteSteps(ProjectRun projectRun)
        {
            CloudTable table = GetStepsTable(projectRun.ProjectName);

            var steps = await GetStepEntities(projectRun);
            //TODO loop batch deletes
            if (steps.Count > 0)
            {
                TableBatchOperation batchop = new TableBatchOperation();

                foreach (TableEntity entity in steps)
                {
                    TableOperation delop = TableOperation.Delete(entity);
                    batchop.Add(delop);
                    //Console.WriteLine("{0}, {1}\t{2}\t{3}", entity.PartitionKey, entity.RowKey,
                    //    entity.PartitionKey, entity.RowKey);
                }

                await table.ExecuteBatchAsync(batchop);
            }
        }

        public static async Task UpdateStatetEntity(string projectName, int state)
        {
            CloudTable table = GetProjectControlTable();
            ProjectControlEntity projectControlEntity = new ProjectControlEntity(projectName, state);
            TableOperation mergeOperation = TableOperation.InsertOrMerge(projectControlEntity);

            await table.ExecuteAsync(mergeOperation);
        }

        /// <summary>
        /// Called on start to create needed tables
        /// </summary>
        public static async Task CreateTables(string projectName)
        {
            // StepsMyProject for step config
            CloudTable stepsTable = GetStepsTable(projectName);

            // MicroflowLog table
            CloudTable logOrchestrationTable = GetLogOrchestrationTable();

            // MicroflowLog table
            CloudTable logStepsTable = GetLogStepsTable();

            // ErrorMyProject table
            CloudTable errorsTable = GetErrorsTable();

            //var delLogTableTask = await logTable.DeleteIfExistsAsync();

            // ProjectControlTable
            CloudTable projectTable = GetProjectControlTable();

            var t1 = stepsTable.CreateIfNotExistsAsync();
            var t2 = logOrchestrationTable.CreateIfNotExistsAsync();
            var t3 = projectTable.CreateIfNotExistsAsync();
            var t4 = logStepsTable.CreateIfNotExistsAsync();
            var t5 = errorsTable.CreateIfNotExistsAsync();

            await t1;
            await t2;
            await t3;
            await t4;
            await t5;
        }

        /// <summary>
        /// Called on start to insert needed step configs
        /// </summary>
        public static async Task InsertStep(HttpCall stepEnt, CloudTable table)
        {
            TableOperation op = TableOperation.InsertOrReplace(stepEnt);

            await table.ExecuteAsync(op);
        }

        private static CloudTable GetErrorsTable()
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"MicroflowLogErrors");
        }

        public static CloudTable GetStepsTable(string projectName)
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"MicroflowSteps{projectName}");
        }

        private static CloudTable GetProjectControlTable()
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"MicroflowProjectControl");
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

            return storageAccount.CreateCloudTableClient(new TableClientConfiguration());
        }
    }
}
