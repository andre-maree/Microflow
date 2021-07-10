﻿namespace Microflow.Models
{
    public interface IHttpCall : IStepEntity
    {
        int ActionTimeoutSeconds { get; set; }
        bool AsynchronousPollingEnabled { get; set; }
        string CallBackAction { get; set; }
        string CalloutUrl { get; set; }
        string GlobalKey { get; set; }
        bool IsHttpGet { get; set; }
        string MainOrchestrationId { get; set; }
        string RunId { get; set; }
        string StepId { get; set; }
        bool StopOnActionFailed { get; set; }
    }
}