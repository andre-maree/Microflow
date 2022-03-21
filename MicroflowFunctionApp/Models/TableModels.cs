using System;
using Azure;
using Azure.Data.Tables;

namespace Microflow.Models
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
    /// This is used to check if all parents have completed
    /// </summary>
    public class ParentCountCompletedEntity : ITableEntity
    {
        public ParentCountCompletedEntity() { }

        public ParentCountCompletedEntity(string runId, string stepId)
        {
            PartitionKey = runId;
            RowKey = stepId;
        }

        public int ParentCountCompleted { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    #endregion
}
