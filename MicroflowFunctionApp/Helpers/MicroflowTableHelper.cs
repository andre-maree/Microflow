using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MicroflowModels;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.Helpers
{
    public static class MicroflowTableHelper
    {
        public async static Task LogStep(LogStepEntity logEntity)
        {
            CloudTable table = null;
            try
            {
                table = GetLogStepsTable();

                TableOperation mergeOperation = TableOperation.InsertOrReplace(logEntity);
                await table.ExecuteAsync(mergeOperation);
            }
            catch (StorageException ex)
            {

            }
        }

        public async static Task LogOrchestration(LogOrchestrationEntity logEntity)
        {
            CloudTable table = null;
            try
            {
                table = GetLogOrchestrationTable();

                TableOperation mergeOperation = TableOperation.InsertOrReplace(logEntity);
                await table.ExecuteAsync(mergeOperation);
            }
            catch (StorageException ex)
            {
                
            }
        }


        public static async Task Pause(ProjectControlEntity projectControlEntity)
        {
            try
            {
                CloudTable table = GetProjectControlTable();

                TableOperation mergeOperation = TableOperation.Merge(projectControlEntity);

                await table.ExecuteAsync(mergeOperation);
            }
            catch (StorageException)
            {
                throw;
            }
        }

        public static async Task<ProjectControlEntity> GetProjectControl(string projectId)
        {
            try
            {
                CloudTable table = GetProjectControlTable();

                TableOperation mergeOperation = TableOperation.Retrieve<ProjectControlEntity>(projectId, "0");

                TableResult result = await table.ExecuteAsync(mergeOperation);
                ProjectControlEntity projectControlEntity = result.Result as ProjectControlEntity;

                return projectControlEntity;
            }
            catch (StorageException)
            {
                throw;
            }
        }

        public static async Task<int> GetState(string projectId)
        {
            try
            {
                CloudTable table = GetProjectControlTable();

                TableOperation mergeOperation = TableOperation.Retrieve<ProjectControlEntity>(projectId, "0");

                TableResult result = await table.ExecuteAsync(mergeOperation);
                ProjectControlEntity projectControlEntity = result.Result as ProjectControlEntity;

                // ReSharper disable once PossibleNullReferenceException
                return projectControlEntity.State;
            }
            catch (StorageException)
            {
                throw;
            }
        }

        public static async Task<HttpCallWithRetries> GetStep(ProjectRun projectRun)
        {
            try
            {
                CloudTable table = GetStepsTable(projectRun.ProjectId);

                TableOperation retrieveOperation = TableOperation.Retrieve<HttpCallWithRetries>($"{projectRun.ProjectId}", $"{projectRun.RunObject.StepId}");
                TableResult result = await table.ExecuteAsync(retrieveOperation);
                HttpCallWithRetries stepEnt = result.Result as HttpCallWithRetries;

                return stepEnt;
            }
            catch (StorageException)
            {
                throw;
            }
        }

        public static async Task UpdateStatetEntity(string projectId, int state)
        {
            CloudTable table = null;
            try
            {
                table = GetProjectControlTable();

                ProjectControlEntity projectControlEntity = new ProjectControlEntity(projectId, state);
                TableOperation mergeOperation = TableOperation.InsertOrMerge(projectControlEntity);
                await table.ExecuteAsync(mergeOperation);
            }
            catch (StorageException ex)
            {
            }
        }

        /// <summary>
        /// Called on start to create needed tables
        /// </summary>
        public static async Task CreateTables(string projectId)
        {
            try
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
            catch (StorageException)
            {
                throw;
            }
        }

        /// <summary>
        /// Called on start to insert needed step configs
        /// </summary>
        public static async Task InsertStep(HttpCall stepEnt, CloudTable table)
        {
            try
            {
                TableOperation op = TableOperation.InsertOrReplace(stepEnt);
                await table.ExecuteAsync(op);
            }
            catch (StorageException)
            {
                throw;
            }
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
