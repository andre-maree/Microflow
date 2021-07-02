using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microflow.Models;
using MicroflowModels;
using Microsoft.Azure.Cosmos.Table;

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

        public static async Task Pause(this ProjectControlEntity projectControlEntity)
        {
            CloudTable table = GetProjectControlTable();
            TableOperation mergeOperation = TableOperation.Merge(projectControlEntity);

            await table.ExecuteAsync(mergeOperation);
        }

        public static string GetProjectAsJson(string projectName)
        {
            List<HttpCallWithRetries> steps = GetStepsHttpCallWithRetries(projectName);
            List<Step> outSteps = new List<Step>();

            for (int i = 1; i < steps.Count; i++)
            {
                HttpCallWithRetries step = steps[i];
                Step newstep = new Step()
                {
                    StepId = Convert.ToInt32(step.RowKey),
                    ActionTimeoutSeconds = step.ActionTimeoutSeconds,
                    StopOnActionFailed = step.StopOnActionFailed,
                    CallbackAction = step.CallBackAction,
                    IsHttpGet = step.IsHttpGet,
                    CalloutUrl = step.Url,
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

                foreach(List<int> s in stepEntList)
                {
                    subStepsList.Add(s[0]);
                }

                newstep.SubSteps = subStepsList;

                outSteps.Add(newstep);
            }

            return JsonSerializer.Serialize(outSteps);
        }

        public static async Task<ProjectControlEntity> GetProjectControl(string projectName)
        {
            CloudTable table = GetProjectControlTable();
            TableOperation mergeOperation = TableOperation.Retrieve<ProjectControlEntity>(projectName, "0");
            TableResult result = await table.ExecuteAsync(mergeOperation);
            ProjectControlEntity projectControlEntity = result.Result as ProjectControlEntity;

            return projectControlEntity;
        }

        public static async Task<int> GetState(string projectName)
        {
            CloudTable table = GetProjectControlTable();
            TableOperation mergeOperation = TableOperation.Retrieve<ProjectControlEntity>(projectName, "0");
            TableResult result = await table.ExecuteAsync(mergeOperation);
            ProjectControlEntity projectControlEntity = result.Result as ProjectControlEntity;

            // ReSharper disable once PossibleNullReferenceException
            return projectControlEntity.State;
        }

        public static List<HttpCallWithRetries> GetStepsHttpCallWithRetries(string projectName)
        {
            CloudTable table = GetStepsTable(projectName);

            TableQuery<HttpCallWithRetries> query = new TableQuery<HttpCallWithRetries>();

            List<HttpCallWithRetries> list = new List<HttpCallWithRetries>();
            //TODO use the async version
            foreach (HttpCallWithRetries httpCallWithRetries in table.ExecuteQuery(query))
            {
                list.Add(httpCallWithRetries);
            }

            return list;
        }

        public static async Task<HttpCallWithRetries> GetStep(this ProjectRun projectRun)
        {
            CloudTable table = GetStepsTable(projectRun.ProjectName);
            TableOperation retrieveOperation = TableOperation.Retrieve<HttpCallWithRetries>($"{projectRun.ProjectName}", $"{projectRun.RunObject.StepId}");
            TableResult result = await table.ExecuteAsync(retrieveOperation);
            HttpCallWithRetries stepEnt = result.Result as HttpCallWithRetries;

            return stepEnt;
        }

        public static List<TableEntity> GetStepEntities(string projectName)
        {
            CloudTable table = GetStepsTable(projectName);

            TableQuery<TableEntity> query = new TableQuery<TableEntity>();

            List<TableEntity> list = new List<TableEntity>();
            //TODO use the async version
            foreach (TableEntity entity in table.ExecuteQuery(query))
            {
                list.Add(entity);
            }

            return list;
        }

        public static async Task DeleteSteps(this ProjectRun projectRun)
        {
            CloudTable table = GetStepsTable(projectRun.ProjectName);

            List<TableEntity> steps = GetStepEntities(projectRun.ProjectName);
            //TODO loop batch deletes
            if (steps.Count > 0)
            {
                TableBatchOperation batchop = new TableBatchOperation();

                foreach (TableEntity entity in steps)
                {
                    TableOperation delop = TableOperation.Delete(entity);
                    batchop.Add(delop);
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

            Task<bool> t1 = stepsTable.CreateIfNotExistsAsync();
            Task<bool> t2 = logOrchestrationTable.CreateIfNotExistsAsync();
            Task<bool> t3 = projectTable.CreateIfNotExistsAsync();
            Task<bool> t4 = logStepsTable.CreateIfNotExistsAsync();
            Task<bool> t5 = errorsTable.CreateIfNotExistsAsync();

            await t1;
            await t2;
            await t3;
            await t4;
            await t5;
        }

        /// <summary>
        /// Called on start to insert needed step configs
        /// </summary>
        public static async Task InsertStep(this HttpCall stepEnt, CloudTable table)
        {
            TableOperation op = TableOperation.InsertOrReplace(stepEnt);

            await table.ExecuteAsync(op);
        }

        #endregion

        #region Get table references

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

        #endregion
    }
}
