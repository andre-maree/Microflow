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

        /// <summary>
        /// Used for step level logging
        /// </summary>
        public class LogErrorEntity : ITableEntity
    {
        public LogErrorEntity() { }

        public LogErrorEntity(string workflowName, int stepNumber, string message, string globalKey, string runId = null)
        {
            PartitionKey = workflowName + "__" + runId;
            RowKey = TableHelpers.GetTableRowKeyDescendingByDate();
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

    /// <summary>
    /// Used for orchestration level logging
    /// </summary>
    //public class LogOrchestrationEntity : ITableEntity
    //{
    //    public LogOrchestrationEntity() { }

    //    public LogOrchestrationEntity(bool isStart, string workflowName, string rowKey, string logMessage, DateTime date, string orchestrationId, string globalKey)
    //    {
    //        PartitionKey = workflowName;
    //        LogMessage = logMessage;
    //        RowKey = rowKey;
    //        OrchestrationId = orchestrationId;
    //        GlobalKey = globalKey;

    //        if (isStart)
    //            StartDate = date;
    //        else
    //            EndDate = date;
    //    }

    //    public string GlobalKey { get; set; }
    //    public string OrchestrationId { get; set; }
    //    public string LogMessage { get; set; }
    //    public DateTime? StartDate { get; set; }
    //    public DateTime? EndDate { get; set; }
    //    public string PartitionKey { get; set; }
    //    public string RowKey { get; set; }
    //    public DateTimeOffset? Timestamp { get; set; }
    //    public ETag ETag { get; set; }
    //}

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
        public string CallbackAction { get; set; }
        public bool StopOnActionFailed { get; set; }
        public int CallbackTimeoutSeconds { get; set; }
        public int CalloutTimeoutSeconds { get; set; }
        public bool IsHttpGet { get; set; }
        public string StepId { get; set; }
        public string ScaleGroupId { get; set; }


        [IgnoreDataMember]
        public string GlobalKey { get; set; }

        [IgnoreDataMember]
        public string RunId { get; set; }

        [IgnoreDataMember]
        public string MainOrchestrationId { get; set; }

        [IgnoreDataMember]
        public string BaseUrl { get; set; }
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
