using System;
using Azure;
using Azure.Data.Tables;

namespace Microflow.MicroflowTableModels
{
    #region TableEntity

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

        public LogStepEntity(bool isStart, string workflowName, string rowKey, int stepNumber, string mainOrchestrationId, string runId, string globalKey, bool? success = null, int? httpStatusCode = null, string message = null)
        {
            PartitionKey = workflowName + "__" + mainOrchestrationId;
            RowKey = rowKey;
            StepNumber = stepNumber;
            GlobalKey = globalKey;
            RunId = runId;
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
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    #endregion
}
