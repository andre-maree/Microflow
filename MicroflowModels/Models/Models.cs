using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MicroflowModels
{
    #region POCOs

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

    public class MicroflowProjectBase
    {
        public string WorkflowName { get; set; }
    }

    public class Microflow : MicroflowProjectBase
    {
        public List<Step> Steps { get; set; }

        [DataMember(Name = "MergeFields", EmitDefaultValue = false)]
        public Dictionary<string, string> MergeFields { get; set; } = new Dictionary<string, string>();

        public MicroflowRetryOptions DefaultRetryOptions { get; set; }

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

        public string StepId { get; set; }
        public int StepNumber { get; set; }
        public string CalloutUrl { get; set; }
        public string WebhookAction { get; set; }
        public string ScaleGroupId { get; set; }
        public bool StopOnActionFailed { get; set; } = true;
        public bool IsHttpGet { get; set; }
        public int CalloutTimeoutSeconds { get; set; } = 1000;
        public int WebhookTimeoutSeconds { get; set; } = 1000;
        public bool AsynchronousPollingEnabled { get; set; } = true;
        public bool ForwardPostData { get; set; }

        [DataMember(Name = "SubSteps", EmitDefaultValue = false)]
        public List<int> SubSteps { get; set; } = new List<int>();

        [DataMember(Name = "RetryOptions", EmitDefaultValue = false)]
        public MicroflowRetryOptions RetryOptions { get; set; }
    }

    public class MicroflowRetryOptions
    {
        public int DelaySeconds { get; set; } = 5;
        public int MaxDelaySeconds { get; set; } = 120;
        public int MaxRetries { get; set; } = 15;
        public double BackoffCoefficient { get; set; } = 5;
        public int TimeOutSeconds { get; set; } = 300;
    }

    #endregion
}
