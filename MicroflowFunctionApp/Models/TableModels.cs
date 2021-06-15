using Microsoft.Azure.Cosmos.Table;
using System.Collections.Generic;

/// <summary>
/// This is where all table entity objects reside
/// </summary>
namespace Microflow
{
    #region TableEntity

    /// <summary>
    /// Base class for http calls
    /// </summary>
    public class LogEntity : TableEntity
    {
        public LogEntity() { }

        public LogEntity(string projectId, string runId, string logMessage)
        {
            PartitionKey = projectId;
            RowKey = runId;
            LogMessage = logMessage;
        }

        public string LogMessage { get; set; }
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
