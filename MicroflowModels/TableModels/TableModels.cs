using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Azure;
using Azure.Data.Tables;
using MicroflowModels.Helpers;

namespace MicroflowModels
{
    #region TableEntity
    
        /// <summary>
        /// Used for step level logging
        /// </summary>
        public class LogErrorEntity : ITableEntity
    {
        public LogErrorEntity() { }

        public LogErrorEntity(string workflowName, int stepNumber, string message, string globalKey, string runId = null)
        {
            PartitionKey = workflowName + "__" + runId;
            RowKey = TableHelper.GetTableRowKeyDescendingByDate();
            StepNumber = stepNumber;
            Message = message;
            Date = DateTime.UtcNow;
            GlobalKey = globalKey;
        }

        public string GlobalKey { get; set; }
        public int StepNumber { get; set; }
        public DateTime Date { get; set; }
        public string Message { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    /// <summary>
    /// Base class for http calls
    /// </summary>
    public class StepEntity : ITableEntity, IStepEntity
    {
        public StepEntity() { }

        public string SubSteps { get; set; }
        public Dictionary<string, string> MergeFields { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    /// <summary>
    /// Basic http call with no retries
    /// </summary>
    public class HttpCall : StepEntity, IHttpCall
    {
        public HttpCall() { }

        public HttpCall(string workflow, string stepNumber, string stepId, string subSteps)
        {
            PartitionKey = workflow;
            RowKey = stepNumber;
            SubSteps = subSteps;
            StepId = stepId;
        }
        public bool AsynchronousPollingEnabled { get; set; }
        public string CalloutUrl { get; set; }
        public string Webhook { get; set; }
        public bool StopOnActionFailed { get; set; }
        public int WebhookTimeoutSeconds { get; set; }
        public int CalloutTimeoutSeconds { get; set; }
        public bool IsHttpGet { get; set; }
        public string StepId { get; set; }
        public string ScaleGroupId { get; set; }
        public bool ForwardPostData { get; set; }


        [IgnoreDataMember]
        public string GlobalKey { get; set; }

        [IgnoreDataMember]
        public string RunId { get; set; }

        [IgnoreDataMember]
        public string MainOrchestrationId { get; set; }
    }

    /// <summary>
    /// Http call with retries
    /// </summary>
    public class HttpCallWithRetries : HttpCall, IHttpCallWithRetries
    {
        public HttpCallWithRetries() { }

        public HttpCallWithRetries(string workflow, string stepNumber, string stepId, string subSteps)
        {
            PartitionKey = workflow;
            RowKey = stepNumber;
            SubSteps = subSteps;
            StepId = stepId;
        }

        // retry options
        public int RetryDelaySeconds { get; set; }
        public int RetryMaxDelaySeconds { get; set; }
        public int RetryMaxRetries { get; set; }
        public double RetryBackoffCoefficient { get; set; }
        public int RetryTimeoutSeconds { get; set; }
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