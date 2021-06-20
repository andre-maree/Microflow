using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;

namespace Microflow.Helpers
{
    public static class MicroflowTableHelper
    {
        public async static Task LogStep(LogStepEntity logEntity)
        {
            CloudTable table = GetLogStepsTable();
            TableOperation mergeOperation = TableOperation.InsertOrReplace(logEntity);

            await table.ExecuteAsync(mergeOperation);
        }

        public async static Task LogOrchestration(LogOrchestrationEntity logEntity)
        {
            CloudTable table = GetLogOrchestrationTable();
            TableOperation mergeOperation = TableOperation.InsertOrReplace(logEntity);

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

        public static async Task UpdateStatetEntity(string projectId, int state)
        {
            CloudTable table = GetProjectControlTable();
            ProjectControlEntity projectControlEntity = new ProjectControlEntity(projectId, state);
            TableOperation mergeOperation = TableOperation.InsertOrMerge(projectControlEntity);

            await table.ExecuteAsync(mergeOperation);
        }

        /// <summary>
        /// Called on start to create needed tables
        /// </summary>
        public static async Task CreateTables(string projectId)
        {
            // StepsMyProject for step config
            CloudTable stepsTable = GetStepsTable(projectId);

            // MicroflowLog table
            CloudTable logOrchestrationTable = GetLogOrchestrationTable();

            // MicroflowLog table
            CloudTable logStepsTable = GetLogStepsTable();

            //var delLogTableTask = await logTable.DeleteIfExistsAsync();

            // ProjectControlTable
            CloudTable projectTable = GetProjectControlTable();

            var t1 = stepsTable.CreateIfNotExistsAsync();
            var t2 = logOrchestrationTable.CreateIfNotExistsAsync();
            var t3 = projectTable.CreateIfNotExistsAsync();
            var t4 = logStepsTable.CreateIfNotExistsAsync();

            await t1;
            await t2;
            await t3;
            await t4;
        }

        /// <summary>
        /// Called on start to insert needed step configs
        /// </summary>
        public static async Task InsertStep(HttpCall stepEnt, CloudTable table)
        {
            TableOperation op = TableOperation.InsertOrReplace(stepEnt);

            await table.ExecuteAsync(op);
        }

        public static CloudTable GetStepsTable(string projectId)
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"Steps{projectId}");
        }

        private static CloudTable GetProjectControlTable()
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"ProjectControl");
        }

        private static CloudTable GetLogOrchestrationTable()
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"LogOrchestration");
        }

        private static CloudTable GetLogStepsTable()
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"LogSteps");
        }

        private static CloudTableClient GetTableClient()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

            return storageAccount.CreateCloudTableClient(new TableClientConfiguration());
        }
    }
}
