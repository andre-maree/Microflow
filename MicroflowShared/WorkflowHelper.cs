#if DEBUG || RELEASE || !DEBUG_NO_UPSERT && !DEBUG_NO_UPSERT_FLOWCONTROL && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_UPSERT && !RELEASE_NO_UPSERT_FLOWCONTROL && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using MicroflowModels;
using MicroflowModels.Helpers;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Net;
using System.Text;
using System.Text.Json;
using static MicroflowModels.Constants;

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

            Microflow microflow;

            try
            {
                // deserialize the workflow json
                microflow = JsonSerializer.Deserialize<Microflow>(content);
            }
            catch (Exception ex)
            {
                HttpResponseMessage resp = new(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(ex.Message)
                };

                try
                {
                    _ = await TableHelper.LogError("workflow deserialization error",
                                                       globalKey,
                                                       null,
                                                       ex);
                }
                catch
                {
                    resp.StatusCode = HttpStatusCode.InternalServerError;
                }

                return resp;
            }

            if (string.IsNullOrWhiteSpace(microflow?.WorkflowName))
            {
                return new(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("No workflow name")
                };
            }
            else if (microflow.Steps?.Count == 0)
            {
                return new(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("No workflow steps")
                };
            }

            //    // create a workflow run
            MicroflowRun workflowRun = new()
            {
                WorkflowName = string.IsNullOrWhiteSpace(microflow.WorkflowVersion)
                                ? microflow.WorkflowName
                                : $"{microflow.WorkflowName}@{microflow.WorkflowVersion}"
            };

            EntityId projStateId = new(MicroflowStateKeys.WorkflowState, workflowRun.WorkflowName);

            try
            {
                Task<EntityStateResponse<int>>? globStateTask = null;

                if (!string.IsNullOrWhiteSpace(globalKey))
                {
                    EntityId globalStateId = new(MicroflowStateKeys.GlobalState, globalKey);
                    globStateTask = client.ReadEntityStateAsync<int>(globalStateId);
                }
                // do not do anything, wait for the stopped workflow to be ready
                Task<EntityStateResponse<int>>? projStateTask = client.ReadEntityStateAsync<int>(projStateId);
                int globState = MicroflowStates.Ready;
                if (globStateTask != null)
                {
                    await globStateTask;
                    globState = globStateTask.Result.EntityState;
                }

                EntityStateResponse<int> projState = await projStateTask;
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
                content.ParseMergeFields(ref microflow);

                await delTask;

                // prepare the workflow by persisting parent info to table storage
                await workflowRun.PrepareWorkflow(microflow);

                microflow.Steps = null;
                microflow.WorkflowName = null;
                string workflowConfigJson = JsonSerializer.Serialize(microflow);

                // create the storage tables for the workflow
                await UpsertWorkflowConfigString(workflowRun.WorkflowName, workflowConfigJson);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Azure.RequestFailedException e)
            {
                HttpResponseMessage resp = new(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(e.Message)
                };

                return resp;
            }
            catch (Exception e)
            {
                HttpResponseMessage resp = new(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(e.Message)
                };

                try
                {
                    _ = await TableHelper.LogError(microflow.WorkflowName
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

        private static async Task UpsertWebhook(List<Task> webhookTasks, Webhook webhookEntity, TableClient webhooksTable)
        {
            if (webhookTasks.Count < 40)
            {
                webhookTasks.Add(webhooksTable.UpsertEntityAsync(webhookEntity));
            }
            else
            {
                Task task = await Task.WhenAny(webhookTasks);
                webhookTasks.Add(webhooksTable.UpsertEntityAsync(webhookEntity));
                webhookTasks.Remove(task);
            }
        }

        /// <summary>
        /// Must be called at least once before a workflow creation or update,
        /// do not call this repeatedly when running multiple concurrent instances,
        /// only call this to create a new workflow or to update an existing 1
        /// Saves step meta data to table storage and read during execution
        /// </summary>
        public static async Task PrepareWorkflow(this MicroflowRun workflowRun, Microflow workflow)
        {
            List<Task> webhookTasks = new();
            List<TableTransactionAction> batch = new();
            List<Task> batchTasks = new();
            TableClient stepsTable = TableHelper.GetStepsTable();
            TableClient webhooksTable = TableHelper.GetWebhooksTable();
            Step stepContainer = new(-1, null);
            StringBuilder sb = new();
            List<Step> steps = workflow.Steps;
            List<(int StepNumber, int ParentCount, int WaitForAllParents)> liParentCounts = new();

            foreach (Step step in steps)
            {
                int count = steps.Count(c => c.SubSteps.Contains(step.StepNumber));
                liParentCounts.Add((step.StepNumber, count, step.WaitForAllParents ? 1 : 0));
            }

            for (int i = 0; i < steps.Count; i++)
            {
                Step step = steps[i];

                if (step.EnableWebhook)
                {
                    if (webhookTasks.Count > 20)
                    {
                        await Task.WhenAny(webhookTasks);
                    }

                    Webhook webhookEntity;

                    step.WebhookId = string.IsNullOrWhiteSpace(step.WebhookId) ? Guid.NewGuid().ToString() : step.WebhookId;

                    if (step.WebhookSubStepsMapping != null && step.WebhookSubStepsMapping.Count > 0)
                    {
                        webhookEntity = new(step.WebhookId, JsonSerializer.Serialize(step.WebhookSubStepsMapping));
                    }
                    else
                    {
                        webhookEntity = new(step.WebhookId, null);
                    }

                    webhookTasks.Add(UpsertWebhook(webhookTasks, webhookEntity, webhooksTable));
                }

                (int StepNumber, int ParentCount, int WaitForAllParents) subInfo = liParentCounts.FirstOrDefault(s => s.StepNumber == step.StepNumber);

                if (subInfo.ParentCount == 0)
                {
                    stepContainer.SubSteps.Add(step.StepNumber);
                }

                foreach (int subId in step.SubSteps)
                {
                    (int StepNumber, int ParentCount, int WaitForAllParents) subInfo2 = liParentCounts.FirstOrDefault(s => s.StepNumber.Equals(subId));
                    //int subParentCount = liParentCounts.FirstOrDefault(s => s.StepNumber.Equals(subId)).ParentCount;

                    sb.Append(subId).Append(',').Append(subInfo2.ParentCount).Append(',').Append(subInfo2.WaitForAllParents).Append(';');
                }

                if (step.RetryOptions != null)
                {
                    HttpCallWithRetries httpCallRetriesEntity = new(workflowRun.WorkflowName, step.StepNumber.ToString(), step.StepId, sb.ToString())
                    {
                        WebhookId = step.WebhookId,
                        EnableWebhook = step.EnableWebhook,
                        WebhookTimeoutSeconds = step.WebhookTimeoutSeconds,
                        StopOnCalloutFailure = step.StopOnCalloutFailure,
                        SubStepsToRunForCalloutFailure = (step.SubStepsToRunForCalloutFailure == null || step.SubStepsToRunForCalloutFailure.Count < 1) ? null : JsonSerializer.Serialize(step.SubStepsToRunForCalloutFailure),
                        SubStepsToRunForWebhookTimeout = (step.SubStepsToRunForWebhookTimeout == null || step.SubStepsToRunForWebhookTimeout.Count < 1) ? null : JsonSerializer.Serialize(step.SubStepsToRunForWebhookTimeout),
                        StopOnWebhookTimeout = step.StopOnWebhookTimeout,
                        CalloutUrl = step.CalloutUrl,
                        CalloutTimeoutSeconds = step.CalloutTimeoutSeconds,
                        IsHttpGet = step.IsHttpGet,
                        AsynchronousPollingEnabled = step.AsynchronousPollingEnabled,
                        ScaleGroupId = step.ScaleGroupId,
                        ForwardResponseData = step.ForwardResponseData,
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
                    HttpCall httpCallEntity = new(workflowRun.WorkflowName, step.StepNumber.ToString(), step.StepId, sb.ToString())
                    {
                        WebhookId = step.WebhookId,
                        EnableWebhook = step.EnableWebhook,
                        StopOnCalloutFailure = step.StopOnCalloutFailure,
                        SubStepsToRunForCalloutFailure = (step.SubStepsToRunForCalloutFailure == null || step.SubStepsToRunForCalloutFailure.Count < 1) ? null : JsonSerializer.Serialize(step.SubStepsToRunForCalloutFailure),
                        WebhookTimeoutSeconds = step.WebhookTimeoutSeconds,
                        SubStepsToRunForWebhookTimeout = (step.SubStepsToRunForWebhookTimeout == null || step.SubStepsToRunForWebhookTimeout.Count < 1) ? null : JsonSerializer.Serialize(step.SubStepsToRunForWebhookTimeout),
                        StopOnWebhookTimeout = step.StopOnWebhookTimeout,
                        CalloutUrl = step.CalloutUrl,
                        CalloutTimeoutSeconds = step.CalloutTimeoutSeconds,
                        IsHttpGet = step.IsHttpGet,
                        AsynchronousPollingEnabled = step.AsynchronousPollingEnabled,
                        ScaleGroupId = step.ScaleGroupId,
                        ForwardResponseData = step.ForwardResponseData
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
                sb.Append(subId).Append(",1,0;");
            }

            HttpCall containerEntity = new(workflowRun.WorkflowName, "-1", null, sb.ToString());

            batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, containerEntity));

            batchTasks.Add(stepsTable.SubmitTransactionAsync(batch));

            await Task.WhenAll(batchTasks);

            await Task.WhenAll(webhookTasks);
        }

        /// <summary>
        /// Parse all the merge fields in the workflow
        /// </summary>
        public static void ParseMergeFields(this string strWorkflow, ref MicroflowModels.Microflow workflow)
        {
            StringBuilder sb = new(strWorkflow);

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

            List<Step> outSteps = new();
            bool skip1st = true;

            await foreach (HttpCallWithRetries step in steps)
            {
                if (skip1st)
                {
                    skip1st = false;
                }
                else
                {
                    Step newstep = new()
                    {
                        StepId = step.RowKey,
                        WebhookTimeoutSeconds = step.WebhookTimeoutSeconds,
                        CalloutTimeoutSeconds = step.CalloutTimeoutSeconds,
                        StopOnWebhookTimeout = step.StopOnWebhookTimeout,
                        //TODO: get WebhookSubStepsMapping
                        //WebhookSubStepsMapping = JsonSerializer.Deserialize<List<SubStepsMappingForActions>>(step.WebhookSubStepsMapping),
                        StopOnCalloutFailure = step.StopOnCalloutFailure,
                        //TODO:
                        //SubStepsToRunForCalloutFailure = (step.SubStepsToRunForCalloutFailure == null || step.SubStepsToRunForCalloutFailure.Count < 1) ? null : JsonSerializer.Serialize(step.SubStepsToRunForCalloutFailure),
                        WebhookId = step.WebhookId,
                        //SubSteps = step.SubSteps,
                        //WaitForAllParents = step.
                        IsHttpGet = step.IsHttpGet,
                        CalloutUrl = step.CalloutUrl,
                        AsynchronousPollingEnabled = step.AsynchronousPollingEnabled,
                        ScaleGroupId = step.ScaleGroupId,
                        ForwardResponseData = step.ForwardResponseData,
                        SubStepsToRunForCalloutFailure = JsonSerializer.Deserialize<List<int>>(step.SubStepsToRunForCalloutFailure),
                        SubStepsToRunForWebhookTimeout = JsonSerializer.Deserialize<List<int>>(step.SubStepsToRunForWebhookTimeout),
                        StepNumber = Convert.ToInt32(step.RowKey),
                        RetryOptions = step.RetryDelaySeconds == 0 ? null : new MicroflowModels.RetryOptions()
                        {
                            BackoffCoefficient = step.RetryBackoffCoefficient,
                            DelaySeconds = step.RetryDelaySeconds,
                            MaxDelaySeconds = step.RetryMaxDelaySeconds,
                            MaxRetries = step.RetryMaxRetries,
                            TimeOutSeconds = step.RetryTimeoutSeconds
                        }
                    };

                    List<int> subStepsList = new();
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

            AsyncPageable<TableEntity> steps = GetStepEntities(workflowRun.WorkflowName);
            List<TableTransactionAction> batch = new();
            List<Task> batchTasks = new();

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

            MicroflowConfigEntity proj = new(workflowName, workflowConfigJson);

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
            TableClient logOrchestrationTable = TableHelper.GetLogOrchestrationTable();

            // MicroflowLog table
            TableClient logStepsTable = TableHelper.GetLogStepsTable();

            // Error table
            TableClient errorsTable = TableHelper.GetErrorsTable();

            // workflow table
            TableClient workflowConfigsTable = GetWorkflowConfigsTable();

            TableClient webhookLogTable = TableHelper.GetLogWebhookTable();

            TableClient webhookTable = TableHelper.GetWebhooksTable();

            Task<Response<TableItem>> t1 = stepsTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t2 = logOrchestrationTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t3 = logStepsTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t4 = errorsTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t5 = workflowConfigsTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t6 = webhookLogTable.CreateIfNotExistsAsync();
            Task<Response<TableItem>> t7 = webhookTable.CreateIfNotExistsAsync();

            await t1;
            await t2;
            await t3;
            await t4;
            await t5;
            await t6;
            await t7;
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