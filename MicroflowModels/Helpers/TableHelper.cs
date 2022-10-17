using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MicroflowModels.Helpers
{
    public static class TableHelper
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

        public static async Task UpsertStep(this HttpCallWithRetries step)
        {
            TableClient tableClient = GetStepsTable();

            await tableClient.UpsertEntityAsync(step);
        }

        public static async Task LogError(this LogErrorEntity logEntity)
        {
            TableClient tableClient = GetErrorsTable();

            await tableClient.UpsertEntityAsync(logEntity);
        }

        public static async Task<HttpResponseMessage> LogError(string workflowName, string globalKey, string runId, Exception e)
        {
            await new LogErrorEntity(workflowName, -999, e.Message, globalKey, runId).LogError();

            HttpResponseMessage resp = new(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(e.Message)
            };

            return resp;
        }

        public static async Task<HttpCallWithRetries> GetStep(this MicroflowRun workflowRun)
        {
            TableClient tableClient = GetStepsTable();

            return await tableClient.GetEntityAsync<HttpCallWithRetries>(workflowRun.WorkflowName, workflowRun.RunObject.StepNumber);
        }

        public static async Task<Webhook> GetWebhook(string webhookId)
        {
            TableClient tableClient = GetWebhooksTable();

            return await tableClient.GetEntityAsync<Webhook>(webhookId, "0", new string[] { "WebhookSubStepsMapping" });
        }

        public static TableClient GetErrorsTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogErrors");
        }

        public static TableClient GetStepsTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowStepConfigs");
        }

        public static TableServiceClient GetTableClient()
        {
            return new TableServiceClient(Environment.GetEnvironmentVariable("MicroflowStorage"));
        }

        public static TableClient GetLogWebhookTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogWebhooks");
        }

        public static TableClient GetWebhooksTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowWebhookConfigs");
        }
        public static TableClient GetLogOrchestrationTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogOrchestrations");
        }

        public static TableClient GetLogStepsTable()
        {
            TableServiceClient tableClient = GetTableClient();

            return tableClient.GetTableClient($"MicroflowLogSteps");
        }
    }
}
