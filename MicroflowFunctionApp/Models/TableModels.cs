using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using Microflow.Helpers;

/// <summary>
/// This is where all table entity objects reside
/// </summary>
namespace Microflow
{
    #region TableEntity

    /// <summary>
    /// Used for step level logging
    /// </summary>
    public class LogErrorEntity : TableEntity
    {
        public LogErrorEntity() { }

        public LogErrorEntity(string projectName, string message, string runId = null, int? stepId = null)
        {
            PartitionKey = projectName + "__" + runId;
            RowKey = MicroflowTableHelper.GetTableRowKeyDescendingByDate();
            StepId = stepId;
            Message = message;
            Date = DateTime.UtcNow;
        }

        public int? StepId { get; set; }
        public DateTime Date { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Used for step level logging
    /// </summary>
    public class LogStepEntity : TableEntity
    {
        public LogStepEntity() { }

        public LogStepEntity(bool isStart, string projectName, string stepId, string runId)
        {
            PartitionKey = projectName + "__" + runId;
            RowKey = stepId;
            if (isStart)
                StartDate = DateTime.UtcNow;
            else
                EndDate = DateTime.UtcNow;
        }
        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }
    }

    /// <summary>
    /// Used for orchestration level logging
    /// </summary>
    public class LogOrchestrationEntity : TableEntity
    {
        public LogOrchestrationEntity() { }

        public LogOrchestrationEntity(bool isStart, string projectName, string rowKey, string logMessage, DateTime date)
        {
            PartitionKey = projectName;
            LogMessage = logMessage;
            RowKey = rowKey;

            if (isStart)
                StartDate = date;
            else
                EndDate = date;
        }

        public string LogMessage { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }
    }

    /// <summary>
    /// Base class for http calls
    /// </summary>
    public class StepEntity : TableEntity
    {
        public StepEntity() { }

        public string SubSteps { get; set; }

        public Dictionary<string, string> MergeFields { get; set; }
    }

    /// <summary>
    /// Basic http call with no retries
    /// </summary>
    public class HttpCall : StepEntity
    {
        public HttpCall() { }

        public HttpCall(string project, int stepId, string subSteps)
        {
            PartitionKey = project;
            RowKey = $"{stepId}";
            SubSteps = subSteps;
        }

        public string Url { get; set; }
        public string CallBackAction { get; set; }
        public bool StopOnActionFailed { get; set; }
        public int ActionTimeoutSeconds { get; set; }
        public bool IsHttpGet { get; set; }

        [IgnoreProperty]
        public string RunId { get; set; }

        [IgnoreProperty]
        public string MainOrchestrationId { get; set; }
    }

    /// <summary>
    /// Http call with retries
    /// </summary>
    public class HttpCallWithRetries : HttpCall
    {
        public HttpCallWithRetries() { }

        public HttpCallWithRetries(string project, int stepId, string subSteps)
        {
            PartitionKey = project;
            RowKey = $"{stepId}";
            SubSteps = subSteps;
        }

        // retry options
        public int Retry_DelaySeconds { get; set; }
        public int Retry_MaxDelaySeconds { get; set; }
        public int Retry_MaxRetries { get; set; }
        public double Retry_BackoffCoefficient { get; set; }
        public int Retry_TimeoutSeconds { get; set; }
    }

    /// <summary>
    /// This is used to check if all parents have completed
    /// </summary>
    public class ParentCountCompletedEntity : TableEntity
    {
        public ParentCountCompletedEntity() { }

        public ParentCountCompletedEntity(string runId, string stepId)
        {
            PartitionKey = runId;
            RowKey = stepId;
        }

        public int ParentCountCompleted { get; set; }
    }

    /// <summary>
    /// This is used to store higher level project execution state data
    /// </summary>
    public class ProjectControlEntity : TableEntity
    {
        public ProjectControlEntity() { }

        public ProjectControlEntity(string projectId, int state)
        {
            PartitionKey = projectId;
            RowKey = "0";
            State = state;
        }

        public int State { get; set; }
        public int PausedStepId { get; set; }
    }

    #endregion
}
