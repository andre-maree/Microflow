using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MicroflowModels
{
    #region POCOs

    public class MicroflowPostData
    {
        public string ProcessId { get; set; }
        public string StepId { get; set; }
    }

    public class Project
    {
        public string ProjectName { get; set; }
        public Step AllSteps { get; set; }
        public int Loop { get; set; } = 1;

        [DataMember(Name = "MergeFields", EmitDefaultValue = false)]
        public Dictionary<string, string> MergeFields { get; set; } = new Dictionary<string, string>();

        public MicroflowRetryOptions DefaultRetryOptions { get; set; }
    }

    public class Step
    {
        public Step() { }

        public Step(int stepId, string calloutUrl)
        {
            StepId = stepId;
            CalloutUrl = calloutUrl;
        }

        public Step(int stepId, List<Step> subSteps)
        {
            StepId = stepId;
            SubSteps = subSteps;
        }

        public int StepId { get; set; }
        public string CalloutUrl { get; set; }
        public string CallbackAction { get; set; }
        public bool StopOnActionFailed { get; set; } = true;
        public int ActionTimeoutSeconds { get; set; } = 1000;

        [DataMember(Name = "SubSteps", EmitDefaultValue = false)]
        public List<Step> SubSteps { get; set; } = new List<Step>();

        [DataMember(Name = "RetryOptions", EmitDefaultValue = false)]
        public MicroflowRetryOptions RetryOptions { get; set; }
    }

    public class MicroflowRetryOptions
    {
        public int DelaySeconds { get; set; }
        public int MaxDelaySeconds { get; set; }
        public int MaxRetries { get; set; }
        public double BackoffCoefficient { get; set; } = 1;
        public int TimeOutSeconds { get; set; }
    }

    #endregion
}
