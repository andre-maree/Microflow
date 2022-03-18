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
    /// Used when locking for parent step concurrency count checking
    /// </summary>
    public class CanExecuteNowObject
    {
        public string RunId { get; set; }
        public string StepNumber { get; set; }
        public int ParentCount { get; set; }
        public string ScaleGroupId { get; set; }
        public string WorkflowName { get; set; }
    }

    /// <summary>
    /// Used when locking for parent step concurrency count checking
    /// </summary>
    public class CanExecuteResult
    {
        public bool CanExecute { get; set; }
        public string StepNumber { get; set; }
    }

    #endregion
}
