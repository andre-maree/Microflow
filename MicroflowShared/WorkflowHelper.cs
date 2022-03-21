#if !DEBUG_NOUPSERT && !DEBUG_NOUPSERT_NOFLOWCONTROL && !DEBUG_NOUPSERT_NOFLOWCONTROL && !DEBUG_NOUPSERT_NOFLOWCONTROL_NOSCALEGROUPS
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using MicroflowModels;
using MicroflowModels.Helpers;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Net;
using System.Text;
using System.Text.Json;
using static MicroflowModels.Constants.Constants;

namespace MicroflowShared
{
    public static class WorkflowHelper
    {
        /// <summary>
        /// From the api call
        /// </summary>
        public static async Task<HttpResponseMessage> UpsertWorkflow(this IDurableEntityClient client,
                                                                           string content,
                                                                           string globalKey)
        {
            bool doneReadyFalse = false;

            // deserialize the workflow json
            Microflow workflow = JsonSerializer.Deserialize<Microflow>(content);

            //    // create a workflow run
            MicroflowRun workflowRun = new MicroflowRun()
            {
                WorkflowName = workflow.WorkflowName,
                Loop = workflow.Loop
            };

            EntityId projStateId = new EntityId(MicroflowStateKeys.WorkflowState, workflowRun.WorkflowName);

            try
            {
                Task<EntityStateResponse<int>> globStateTask = null;

                if (!string.IsNullOrWhiteSpace(globalKey))
                {
                    EntityId globalStateId = new EntityId(MicroflowStateKeys.GlobalState, globalKey);
                    globStateTask = client.ReadEntityStateAsync<int>(globalStateId);
                }
                // do not do anything, wait for the stopped workflow to be ready
                var projStateTask = client.ReadEntityStateAsync<int>(projStateId);
                int globState = MicroflowStates.Ready;
                if (globStateTask != null)
                {
                    await globStateTask;
                    globState = globStateTask.Result.EntityState;
                }

                var projState = await projStateTask;
                if (projState.EntityState != MicroflowStates.Ready || globState != MicroflowStates.Ready)
                {
                    return new HttpResponseMessage(HttpStatusCode.Locked);
                }

                // set workflow ready to false
                await client.SignalEntityAsync(projStateId, MicroflowControlKeys.Pause);
                doneReadyFalse = true;

                // create the storage tables for the workflow
                await CreateTables();

                //  clear step table data
                Task delTask = workflowRun.DeleteSteps();

                //    // parse the mergefields
                content.ParseMergeFields(ref workflow);

                await delTask;

                // prepare the workflow by persisting parent info to table storage
                await workflowRun.PrepareWorkflow(workflow);

                workflow.Steps = null;
                workflow.WorkflowName = null;
                string workflowConfigJson = JsonSerializer.Serialize(workflow);

                // create the storage tables for the workflow
                await UpsertWorkflowConfigString(workflowRun.WorkflowName, workflowConfigJson);

                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }
            catch (Azure.RequestFailedException e)
            {
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(e.Message)
                };

                return resp;
            }
            catch (Exception e)
            {
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(e.Message)
                };

                try
                {
                    _ = await TableHelper.LogError(workflow.WorkflowName
                                                       ?? "no workflow",
                                                       workflowRun.RunObject.GlobalKey,
                                                       workflowRun.RunObject.RunId,
                                                       e);
                }
                catch
                {
                    resp.StatusCode = HttpStatusCode.InternalServerError;
                }

                return resp;
            }
            finally
            {
                // if workflow ready was set to false, always set it to true
                if (doneReadyFalse)
                {
                    await client.SignalEntityAsync(projStateId, MicroflowControlKeys.Ready);
                }
            }
        }

        /// <summary>
        /// Must be called at least once before a workflow creation or update,
        /// do not call this repeatedly when running multiple concurrent instances,
        /// only call this to create a new workflow or to update an existing 1
        /// Saves step meta data to table storage and read during execution
        /// </summary>
        public static async Task PrepareWorkflow(this MicroflowRun workflowRun, MicroflowModels.Microflow workflow)
        {
            List<TableTransactionAction> batch = new List<TableTransactionAction>();
            List<Task> batchTasks = new List<Task>();
            TableClient stepsTable = TableHelper.GetStepsTable();
            Step stepContainer = new Step(-1, null);
            StringBuilder sb = new StringBuilder();
            List<Step> steps = workflow.Steps;
            List<(int StepNumber, int ParentCount)> liParentCounts = new List<(int, int)>();

            foreach (Step step in steps)
            {
                int count = steps.Count(c => c.SubSteps.Contains(step.StepNumber));
                liParentCounts.Add((step.StepNumber, count));
            }

            for (int i = 0; i < steps.Count; i++)
            {
                Step step = steps.ElementAt(i);

                int parentCount = liParentCounts.FirstOrDefault(s => s.StepNumber == step.StepNumber).ParentCount;

                if (parentCount == 0)
                {
                    stepContainer.SubSteps.Add(step.StepNumber);
                }

                foreach (int subId in step.SubSteps)
                {
                    int subParentCount = liParentCounts.FirstOrDefault(s => s.StepNumber.Equals(subId)).ParentCount;

                    sb.Append(subId).Append(',').Append(subParentCount).Append(';');
                }

                if (step.RetryOptions != null)
                {
                    HttpCallWithRetries httpCallRetriesEntity = new HttpCallWithRetries(workflowRun.WorkflowName, step.StepNumber.ToString(), step.StepId, sb.ToString())
                    {
                        CallbackAction = step.CallbackAction,
                        StopOnActionFailed = step.StopOnActionFailed,
                        CalloutUrl = step.CalloutUrl,
                        CallbackTimeoutSeconds = step.CallbackTimeoutSeconds,
                        CalloutTimeoutSeconds = step.CalloutTimeoutSeconds,
                        IsHttpGet = step.IsHttpGet,
                        AsynchronousPollingEnabled = step.AsynchronousPollingEnabled,
                        ScaleGroupId = step.ScaleGroupId
                    };

                    httpCallRetriesEntity.RetryDelaySeconds = step.RetryOptions.DelaySeconds;
                    httpCallRetriesEntity.RetryMaxDelaySeconds = step.RetryOptions.MaxDelaySeconds;
                    httpCallRetriesEntity.RetryMaxRetries = step.RetryOptions.MaxRetries;
                    httpCallRetriesEntity.RetryTimeoutSeconds = step.RetryOptions.TimeOutSeconds;
                    httpCallRetriesEntity.RetryBackoffCoefficient = step.RetryOptions.BackoffCoefficient;

                    // batchop
                    batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, httpCallRetriesEntity));
                }
                else
                {
                    HttpCall httpCallEntity = new HttpCall(workflowRun.WorkflowName, step.StepNumber.ToString(), step.StepId, sb.ToString())
                    {
                        CallbackAction = step.CallbackAction,
                        StopOnActionFailed = step.StopOnActionFailed,
                        CalloutUrl = step.CalloutUrl,
                        CallbackTimeoutSeconds = step.CallbackTimeoutSeconds,
                        CalloutTimeoutSeconds = step.CalloutTimeoutSeconds,
                        IsHttpGet = step.IsHttpGet,
                        AsynchronousPollingEnabled = step.AsynchronousPollingEnabled,
                        ScaleGroupId = step.ScaleGroupId
                    };

                    // batchop
                    batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, httpCallEntity));
                }

                sb.Clear();

                if (batch.Count == 100)
                {
                    batchTasks.Add(stepsTable.SubmitTransactionAsync(batch));
                    batch = new List<TableTransactionAction>();
                }
            }

            foreach (int subId in stepContainer.SubSteps)
            {
                sb.Append(subId).Append(",1;");
            }

            HttpCall containerEntity = new HttpCall(workflowRun.WorkflowName, "-1", null, sb.ToString());

            batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, containerEntity));

            batchTasks.Add(stepsTable.SubmitTransactionAsync(batch));

            TableEntity mergeFieldsEnt = new TableEntity($"{workflowRun.WorkflowName}_MicroflowMergeFields", "");
            await stepsTable.UpsertEntityAsync(mergeFieldsEnt);

            await Task.WhenAll(batchTasks);
        }

        /// <summary>
        /// Parse all the merge fields in the workflow
        /// </summary>
        public static void ParseMergeFields(this string strWorkflow, ref MicroflowModels.Microflow workflow)
        {
            StringBuilder sb = new StringBuilder(strWorkflow);

            foreach (KeyValuePair<string, string> field in workflow.MergeFields)
            {
                sb.Replace("{" + field.Key + "}", field.Value);
            }

            workflow = JsonSerializer.Deserialize<Microflow>(sb.ToString());
        }

        #region Table operations

        // TODO: move out to api app
        public static async Task<string> GetWorkflowJson(string workflowName)
        {
            AsyncPageable<HttpCallWithRetries> steps = GetStepsHttpCallWithRetries(workflowName);

            List<Step> outSteps = new List<Step>();
            bool skip1st = true;

            await foreach (HttpCallWithRetries step in steps)
            {
                if (skip1st)
                {
                    skip1st = false;
                }
                else
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
                        AsynchronousPollingEnabled = step.AsynchronousPollingEnabled,
                        ScaleGroupId = step.ScaleGroupId,
                        StepNumber = Convert.ToInt32(step.RowKey),
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
                    ;
                    string[] stepsAndCounts = step.SubSteps.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < stepsAndCounts.Length; i = i + 2)
                    {
                        subStepsList.Add(Convert.ToInt32(stepsAndCounts[i]));
                    }

                    newstep.SubSteps = subStepsList;

                    outSteps.Add(newstep);
                }
            }

            TableClient wfConfigsTable = GetWorkflowConfigsTable();

            MicroflowConfigEntity projConfig = await wfConfigsTable.GetEntityAsync<MicroflowConfigEntity>(workflowName, "0");

            Microflow proj = JsonSerializer.Deserialize<Microflow>(projConfig.Config);
            proj.WorkflowName = workflowName;
            proj.Steps = outSteps;

            return JsonSerializer.Serialize(proj);
        }

        public static AsyncPageable<HttpCallWithRetries> GetStepsHttpCallWithRetries(string workflowName)
        {
            TableClient tableClient = TableHelper.GetStepsTable();

            return tableClient.QueryAsync<HttpCallWithRetries>(filter: $"PartitionKey eq '{workflowName}'");
        }



        public static AsyncPageable<TableEntity> GetStepEntities(string workflowName)
        {
            TableClient tableClient = TableHelper.GetStepsTable();

            return tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{workflowName}'", select: new List<string>() { "PartitionKey", "RowKey" });
        }

        public static async Task DeleteSteps(this MicroflowRun workflowRun)
        {
            TableClient tableClient = TableHelper.GetStepsTable();

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
        public static async Task UpsertWorkflowConfigString(string workflowName, string workflowConfigJson)
        {
            TableClient projTable = GetWorkflowConfigsTable();

            MicroflowConfigEntity proj = new MicroflowConfigEntity(workflowName, workflowConfigJson);

            await projTable.UpsertEntityAsync(proj);
        }

        /// <summary>
        /// Called on start to create needed tables
        /// </summary>
        public static async Task CreateTables()
        {
            // StepsMyworkflow for step config
            TableClient stepsTable = TableHelper.GetStepsTable();

            // MicroflowLog table
            TableClient logOrchestrationTable = TableReferences.GetLogOrchestrationTable();

            // MicroflowLog table
            TableClient logStepsTable = TableReferences.GetLogStepsTable();

            // Error table
            TableClient errorsTable = TableHelper.GetErrorsTable();

            // workflow table
            TableClient workflowConfigsTable = GetWorkflowConfigsTable();

            Task<Response<TableItem>> t1 = stepsTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t2 = logOrchestrationTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t3 = logStepsTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t4 = errorsTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t5 = workflowConfigsTable.CreateIfNotExistsAsync();

            await t1;
            await t2;
            await t3;
            await t4;
            await t5;
        }

        private static TableClient GetWorkflowConfigsTable()
        {
            TableServiceClient tableClient = TableHelper.GetTableClient();

            return tableClient.GetTableClient($"MicroflowWorkflowConfigs");
        }

        /// <summary>
        /// Used to save and get workflow additional config
        /// </summary>
        public class MicroflowConfigEntity : ITableEntity
        {
            public MicroflowConfigEntity() { }

            public MicroflowConfigEntity(string workflowName, string config)
            {
                PartitionKey = workflowName;
                RowKey = "0";
                Config = config;
            }

            public string Config { get; set; }
            public string PartitionKey { get; set; }
            public string RowKey { get; set; }
            public DateTimeOffset? Timestamp { get; set; }
            public ETag ETag { get; set; }
        }

        #endregion
    }
}
#endif