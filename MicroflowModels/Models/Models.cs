﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MicroflowModels
{
    #region POCOs

    public enum CalloutOrWebhook
    {
        Callout,
        Webhook
    }

    public class ScaleGroupState
    {
        public int ScaleGroupMaxConcurrentInstanceCount { get; set; }
        public int PollingIntervalSeconds { get; set; }
        public int PollingIntervalMaxSeconds { get; set; }
        public int PollingMaxHours { get; set; }
    }

    public class SubStepsMappingForActions
    {
        public string WebhookAction { get; set; }
        public List<int> SubStepsToRunForAction { get; set; } = new();
    }

    public class MicroflowPostData
    {
        public string WorkflowName { get; set; }
        public string MainOrchestrationId { get; set; }
        public string SubOrchestrationId { get; set; }
        public string Webhook { get; set; }
        public string RunId { get; set; }
        public int StepNumber { get; set; }
        public string StepId { get; set; }
        public string GlobalKey { get; set; }
        public string PostData { get; set; }
    }

    public class MicroflowBase
    {
        public string WorkflowName { get; set; }
    }

    public class Microflow : MicroflowBase
    {
        public List<Step> Steps { get; set; }

        [DataMember(Name = "MergeFields", EmitDefaultValue = false)]
        public Dictionary<string, string> MergeFields { get; set; } = new Dictionary<string, string>();

        public RetryOptions DefaultRetryOptions { get; set; }

        public string WorkflowVersion { get;set; }
    }

    public class Step
    {
        public Step() { }

        public Step(int stepNumber, string calloutUrl, string stepId = null)
        {
            if (stepId == null)
            {
                StepId = $"{stepNumber}_{Guid.NewGuid()}";
            }
            else
            {
                StepId = stepId;
            }
            CalloutUrl = calloutUrl;
            StepNumber = stepNumber;
        }

        public Step(string stepId, List<int> subSteps)
        {
            StepId = stepId;
            SubSteps = subSteps;
        }

        public int StepNumber { get; set; }
        public string StepId { get; set; }

        [DataMember(Name = "SubSteps", EmitDefaultValue = false)]
        public List<int> SubSteps { get; set; } = new List<int>();

        [DataMember(Name = "WaitForAllParents", EmitDefaultValue = false)]
        public bool WaitForAllParents { get; set; } = true;

        public string CalloutUrl { get; set; }
        public int CalloutTimeoutSeconds { get; set; } = 1000;
        public bool StopOnCalloutFailure { get; set; }
        public List<int> SubStepsToRunForCalloutFailure { get; set; }
        public bool IsHttpGet { get; set; }
        public bool EnableWebhook { get; set; }
        public string WebhookId { get; set; }
        public bool StopOnWebhookTimeout { get; set; } = true;
        public List<int> SubStepsToRunForWebhookTimeout { get; set; }
        public int WebhookTimeoutSeconds { get; set; } = 1000;

        [DataMember(Name = "WebhookSubStepsMapping", EmitDefaultValue = false)]
        public List<SubStepsMappingForActions> WebhookSubStepsMapping { get; set; }

        public string ScaleGroupId { get; set; }
        public bool AsynchronousPollingEnabled { get; set; } = true;
        public bool ForwardResponseData { get; set; }

        [DataMember(Name = "RetryOptions", EmitDefaultValue = false)]
        public RetryOptions RetryOptions { get; set; }
    }

    public class RetryOptions
    {
        public int DelaySeconds { get; set; } = 5;
        public int MaxDelaySeconds { get; set; } = 120;
        public int MaxRetries { get; set; } = 15;
        public double BackoffCoefficient { get; set; } = 5;
        public int TimeOutSeconds { get; set; } = 300;
    }

    #endregion
}
