﻿using System.Collections.Generic;

namespace MicroflowModels
{
    public interface IHttpCall : IStepEntity
    {
        int WebhookTimeoutSeconds { get; set; }
        bool AsynchronousPollingEnabled { get; set; }
        string Webhook { get; set; }
        string CalloutUrl { get; set; }
        string GlobalKey { get; set; }
        bool IsHttpGet { get; set; }
        string MainOrchestrationId { get; set; }
        string RunId { get; set; }
        string StepId { get; set; }
        bool StopOnActionFailed { get; set; }
        string ScaleGroupId { get; set; }
        string SubStepsToRunForWebhookTimeout { get; set; }
        bool ForwardResponseData { get; set; }
    }
}