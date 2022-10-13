using System;
using Azure;
using Azure.Data.Tables;

namespace Microflow.MicroflowTableModels
{
    #region TableEntity

    public class WebhookEntity : ITableEntity
    {
        public WebhookEntity() { }

        public WebhookEntity(string webhookId, string webhookSubStepsMapping)
        {
            PartitionKey = webhookId;
            RowKey = "0";
            WebhookSubStepsMapping = webhookSubStepsMapping;
        }

        public string WebhookSubStepsMapping { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    public class LogWebhookEntity : ITableEntity
    {
        public LogWebhookEntity() { }

        public LogWebhookEntity(bool isCreate, string workflowName, string rowKey, DateTime date, string runId = "", string action = "")
        {
            PartitionKey = workflowName;
            RowKey = rowKey; 

            if(isCreate)
            {
                CreateDate = date;
                RunId = runId;
            }
            else
            {
                ActionDate = date;
                Action = action;
            }
        }

        public string Action { get; set; }
        public string RunId { get; set; }
        public DateTime? CreateDate { get; set; }
        public DateTime? ActionDate { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    /// <summary>
    /// Used for orchestration level logging
    /// </summary>
    public class LogOrchestrationEntity : ITableEntity
    {
        public LogOrchestrationEntity() { }

        public LogOrchestrationEntity(bool isStart, string workflowName, string rowKey, string logMessage, DateTime date, string orchestrationId, string globalKey)
        {
            PartitionKey = workflowName;
            LogMessage = logMessage;
            RowKey = rowKey;
            OrchestrationId = orchestrationId;
            GlobalKey = globalKey;

            if (isStart)
                StartDate = date;
            else
                EndDate = date;
        }

        public string GlobalKey { get; set; }
        public string OrchestrationId { get; set; }
        public string LogMessage { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    /// <summary>
    /// Used for step level logging
    /// </summary>
    public class LogStepEntity : ITableEntity
    {
        public LogStepEntity() { }

        public LogStepEntity(bool isStart,
                             string workflowName,
                             string rowKey,
                             int stepNumber,
                             string mainOrchestrationId,
                             string runId,
                             string globalKey,
                             string? calloutUrl = null,
                             bool? success = null,
                             int? httpStatusCode = null,
                             string message = null,
                             string? subOrchestrationId = null,
                             string? webhookId = null)
        {
            PartitionKey = workflowName + "__" + mainOrchestrationId;
            RowKey = rowKey;
            StepNumber = stepNumber;
            GlobalKey = globalKey;
            RunId = runId;
            CalloutUrl = calloutUrl;
            SubOrchestrationId = subOrchestrationId;

            if (isStart)
                StartDate = DateTime.UtcNow;
            else
            {
                Success = success;
                HttpStatusCode = httpStatusCode;
                Message = message;
                EndDate = DateTime.UtcNow;
            }
        }

        public bool? Success { get; set; }
        public int? HttpStatusCode { get; set; }
        public string Message { get; set; }
        public int StepNumber { get; set; }
        public string RunId { get; set; }
        public string GlobalKey { get; set; }
        public string CalloutUrl { get; set; }
        public string SubOrchestrationId { get; set; }
        public string WebhookId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    #endregion
}
