using System.Collections.Generic;

namespace Microflow.Models
{
    #region POCOs

    /// <summary>
    /// Used to hold Microflow specific http status code results
    /// </summary>
    public class MicroflowHttpResponse : IMicroflowHttpResponse
    {
        public bool Success { get; set; }
        public int HttpResponseStatusCode { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// This is the minimalist workflow object that is passed during execution
    /// </summary>
    public class MicroflowRun : IMicroflowRun
    {
        public string WorkflowName { get; set; }
        public RunObject RunObject { get; set; }
        public int PausedStepId { get; set; }
        public int Loop { get; set; } = 1;
        public int CurrentLoop { get; set; } = 1;
        public string OrchestratorInstanceId { get; set; }
        public string BaseUrl { get; set; }
    }

    /// <summary>
    /// Used when locking for parent step concurrency count checking
    /// </summary>
    public class CanExecuteNowObject
    {
        public string RunId { get; set; }
        public string StepNumber { get; set; }
        public int ParentCount { get; set; }
        public string ScaleGroupId { get; set; }
        public string workflowName { get; set; }
    }

    /// <summary>
    /// Used when locking for parent step concurrency count checking
    /// </summary>
    public class CanExecuteResult
    {
        public bool CanExecute { get; set; }
        public string StepNumber { get; set; }
    }

    /// <summary>
    /// This is the minimalist run object that is passed during execution inside the workflow run object
    /// </summary>
    public class RunObject
    {
        public string RunId { get; set; }
        public string StepNumber { get; set; }
        public string GlobalKey { get; set; }
    }

    #endregion
}
