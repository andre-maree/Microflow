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
    public static class MicroflowHelper
    {
        public async static Task Log(string projectId, string runId, string message)
        {
            CloudTable table = null;
            try
            {
                table = GetLogTable();

                LogEntity logEntity = new LogEntity(projectId, runId, message);
                TableOperation mergeOperation = TableOperation.InsertOrReplace(logEntity);
                await table.ExecuteAsync(mergeOperation);
            }
            catch (StorageException ex)
            {
                
            }
        }

        public static RetryOptions GetRetryOptions(HttpCallWithRetries httpCallWithRetries)
        {
            RetryOptions ops = new RetryOptions(TimeSpan.FromSeconds(httpCallWithRetries.Retry_DelaySeconds), httpCallWithRetries.Retry_MaxRetries);
            ops.RetryTimeout = TimeSpan.FromSeconds(httpCallWithRetries.Retry_TimeoutSeconds);
            ops.MaxRetryInterval = TimeSpan.FromSeconds(httpCallWithRetries.Retry_MaxDelaySeconds);
            ops.BackoffCoefficient = httpCallWithRetries.Retry_BackoffCoefficient;

            return ops;
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

                // RunControlMyProject for parent execution completed count
                CloudTable runTable = GetRunTable(projectId);

                // MicroflowLog table
                CloudTable logTable = GetLogTable();

                //var delLogTableTask = await logTable.DeleteIfExistsAsync();

                // ProjectControlTable
                CloudTable projectTable = GetProjectControlTable();

                var t1 = stepsTable.CreateIfNotExistsAsync();
                var t2 = runTable.CreateIfNotExistsAsync();
                var t3 = logTable.CreateIfNotExistsAsync();
                var t4 = projectTable.CreateIfNotExistsAsync();

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
        public class StepComparer : IEqualityComparer<Step>
        {
            public bool Equals(Step x, Step y)
            {
                return y != null && x != null && x.StepId == y.StepId;
            }

            public int GetHashCode(Step obj)
            {
                return obj.StepId.GetHashCode();
            }
        }

        /// <summary>
        /// Called before a workflow executes and takes the top step and recursives it to insert step configs into table storage
        /// </summary>
        public static async Task PrepareWorkflow(string instanceId, ProjectRun projectRun, Step step1, Dictionary<string, string> mergeFields)
        {
            HashSet<KeyValuePair<int, int>> hsStepCounts = new HashSet<KeyValuePair<int, int>>();
            HashSet<Step> hsSteps = new HashSet<Step>(new StepComparer());

            Local(step1);

            void Local(Step step)
            {
                hsSteps.Add(step);
                if (step.SubSteps != null)
                {
                    foreach (var cstep in step.SubSteps)
                    {
                        hsStepCounts.Add(new KeyValuePair<int, int>(step.StepId, cstep.StepId));
                        Local(cstep);
                    }
                }
                else
                {
                    step.SubSteps = new List<Step>();
                }
            }

            var tasks = new List<Task>();
            var stepsTable = GetStepsTable(projectRun.ProjectId);

            for (int i = 0; i < hsSteps.Count; i++)
            {
                Step step = hsSteps.ElementAt(i);

                step.CalloutUrl = step.CalloutUrl.Replace("<instanceId>", projectRun.RunObject.RunId, StringComparison.OrdinalIgnoreCase);
                step.CalloutUrl = step.CalloutUrl.Replace("<runId>", projectRun.RunObject.RunId, StringComparison.OrdinalIgnoreCase);
                step.CalloutUrl = step.CalloutUrl.Replace("<stepId>", step.StepId.ToString(), StringComparison.OrdinalIgnoreCase);

                List<KeyValuePair<int, int>> substeps = new List<KeyValuePair<int, int>>();

                foreach (var sub in step.SubSteps)
                {
                    var count = hsStepCounts.Count(x => x.Value == sub.StepId);
                    substeps.Add(new KeyValuePair<int, int>(sub.StepId, count));
                }

                if (step.RetryOptions != null)
                {
                    HttpCallWithRetries stentRetries = new HttpCallWithRetries(projectRun.ProjectId, step.StepId, JsonSerializer.Serialize(substeps))
                    { 
                        CallBackAction = step.CallbackAction, 
                        StopOnActionFailed = step.StopOnActionFailed,
                        Url = step.CalloutUrl,
                        ActionTimeoutSeconds = step.ActionTimeoutSeconds
                    };

                    stentRetries.Retry_DelaySeconds = step.RetryOptions.DelaySeconds;
                    stentRetries.Retry_MaxDelaySeconds = step.RetryOptions.MaxDelaySeconds;
                    stentRetries.Retry_MaxRetries = step.RetryOptions.MaxRetries;
                    stentRetries.Retry_TimeoutSeconds = step.RetryOptions.TimeOutSeconds;
                    stentRetries.Retry_BackoffCoefficient = step.RetryOptions.BackoffCoefficient;

                    tasks.Add(InsertStep(stentRetries, stepsTable));
                }
                else
                {
                    HttpCall stent = new HttpCall(projectRun.ProjectId, step.StepId, JsonSerializer.Serialize(substeps))
                    {
                        CallBackAction = step.CallbackAction,
                        StopOnActionFailed = step.StopOnActionFailed,
                        Url = step.CalloutUrl,
                        ActionTimeoutSeconds = step.ActionTimeoutSeconds
                    };

                    tasks.Add(InsertStep(stent, stepsTable));
                }
            }

            await Task.WhenAll(tasks);
        }

        private const string CharList = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";

        public static string CreateSubOrchestrationId()
        {
            Random r = new Random();
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < 6; i++)
            {
                sb.Append(CharList[r.Next(0, 62)]);
            }
            return sb.ToString();
        }
        private static CloudTable GetRunTable(string projectId)
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"RunControl{projectId}");
        }

        private static CloudTable GetStepsTable(string projectId)
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"Steps{projectId}");
        }

        private static CloudTable GetProjectControlTable()
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"ProjectControl");
        }

        private static CloudTable GetLogTable()
        {
            CloudTableClient tableClient = GetTableClient();

            return tableClient.GetTableReference($"MicroflowLog");
        }

        private static CloudTableClient GetTableClient()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

            return storageAccount.CreateCloudTableClient(new TableClientConfiguration());
        }

        public static void ParseMergeFields(string strWorkflow, ref Project project)
        {
            StringBuilder sb = new StringBuilder(strWorkflow);

            foreach (var field in project.MergeFields)
            {
                sb.Replace("{" + field.Key + "}", field.Value);
            }

            sb.Replace("{workflowId}", "");
            sb.Replace("{stepId}", "");

            project = JsonSerializer.Deserialize<Project>(sb.ToString());
        }

        public static DurableHttpRequest GetDurableHttpRequest(HttpCall httpCall, string instanceId)
        {
            httpCall.PartitionKey = instanceId;

            DurableHttpRequest newDurableHttpRequest = new DurableHttpRequest(
                method: HttpMethod.Post,
                uri: new Uri(httpCall.Url),
                timeout: TimeSpan.FromSeconds(httpCall.ActionTimeoutSeconds)
            //headers: durableHttpRequest.Headers,
            //content: durableHttpRequest.Content, // This is the line causing the issue
            //tokenSource: durableHttpRequest.TokenSource
            );

            // Do not copy over the x-functions-key header, as in many cases, the
            // functions key used for the initial request will be a Function-level key
            // and the status endpoint requires a master key.
            newDurableHttpRequest.Headers.Remove("x-functions-key");

            return newDurableHttpRequest;
        }
    }
}
