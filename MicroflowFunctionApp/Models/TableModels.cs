using System;
using System.Collections.Generic;
using Microflow.Helpers;
using Microsoft.Azure.Cosmos.Table;

namespace Microflow.Models
{
    #region TableEntity

    /// <summary>
    /// Used for step level logging
    /// </summary>
    public class LogErrorEntity : TableEntity
    {
        public LogErrorEntity() { }

        public LogErrorEntity(string projectName, int stepNumber, string message, string globalKey, string runId = null)
        {
            PartitionKey = projectName + "__" + runId;
            RowKey = MicroflowTableHelper.GetTableRowKeyDescendingByDate();
            StepNumber = stepNumber;
            Message = message;
            Date = DateTime.UtcNow;
            GlobalKey = globalKey;
        }

        public string GlobalKey { get; set; }
        public int StepNumber { get; set; }
        public DateTime Date { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Used for step level logging
    /// </summary>
    public class LogStepEntity : TableEntity
    {
        public LogStepEntity() { }

        public LogStepEntity(bool isStart, string projectName, string rowKey, int stepNumber, string mainOrchestrationId, string runId, string globalKey, bool? success = null, int? httpStatusCode = null, string message = null)
        {
            PartitionKey = projectName + "__" + mainOrchestrationId;
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
    }

    /// <summary>
    /// Used for orchestration level logging
    /// </summary>
    public class LogOrchestrationEntity : TableEntity
    {
        public LogOrchestrationEntity() { }

        public LogOrchestrationEntity(bool isStart, string projectName, string rowKey, string logMessage, DateTime date, string orchestrationId, string globalKey)
        {
            PartitionKey = projectName;
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
    }

    /// <summary>
    /// Base class for http calls
    /// </summary>
    public class StepEntity : TableEntity, IStepEntity
    {
        public StepEntity() { }

        public string SubSteps { get; set; }
        public Dictionary<string, string> MergeFields { get; set; }
    }

    /// <summary>
    /// Basic http call with no retries
    /// </summary>
    public class HttpCall : StepEntity, IHttpCall
    {
        public HttpCall() { }

        public HttpCall(string project, string stepNumber, string stepId, string subSteps)
        {
            PartitionKey = project;
            RowKey = stepNumber;
            SubSteps = subSteps;
            StepId = stepId;
        }
        public bool AsynchronousPollingEnabled { get; set; }
        public string CalloutUrl { get; set; }
        public string CallBackAction { get; set; }
        public bool StopOnActionFailed { get; set; }
        public int ActionTimeoutSeconds { get; set; }
        public bool IsHttpGet { get; set; }
        public string StepId { get; set; }

        //[IgnoreProperty]
        public string GlobalKey { get; set; }

        [IgnoreProperty]
        public string RunId { get; set; }

        [IgnoreProperty]
        public string MainOrchestrationId { get; set; }
    }

    /// <summary>
    /// Http call with retries
    /// </summary>
    public class HttpCallWithRetries : HttpCall, IHttpCallWithRetries
    {
        public HttpCallWithRetries() { }

        public HttpCallWithRetries(string project, string stepNumber, string stepId, string subSteps)
        {
            PartitionKey = project;
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

        public ProjectControlEntity(string projectName, int state, int loop = 1, string instanceId = null)
        {
            PartitionKey = projectName;
            RowKey = instanceId ?? "0";
            State = state;
            Loop = loop;
        }

        public int Loop { get; set; }
        public int State { get; set; }
        public int PausedStepId { get; set; }
    }

    #endregion
}
