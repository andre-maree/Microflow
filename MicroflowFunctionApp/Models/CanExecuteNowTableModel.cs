using Azure;
using Azure.Data.Tables;
using System;

namespace Microflow.FlowModels
{
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
}
