namespace Microflow
{
    #region POCOs

    /// <summary>
    /// Used to hold Microflow specific http status code results
    /// </summary>
    public class MicroflowHttpResponse
    {
        public bool Success { get; set; }
        public int HttpResponseStatusCode { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// This is the minimalist project object that is passed during execution
    /// </summary>
    public class ProjectRun
    {
        public string ProjectName { get; set; }
        public RunObject RunObject { get; set; }
        public int PausedStepId { get; set; }
        public int Loop { get; set; } = 1;
        public int CurrentLoop { get; set; } = 1;
        public string OrchestratorInstanceId { get; set; }
    }

    /// <summary>
    /// Used when locking for parent step concurrency count checking
    /// </summary>
    public class CanExecuteNowObject
    {
        public string RunId { get; set; }
        public int StepId { get; set; }
        public int ParentCount { get; set; }
        public string ProjectName { get; set; }
    }

    /// <summary>
    /// Used when locking for parent step concurrency count checking
    /// </summary>
    public class CanExecuteResult
    {
        public bool CanExecute { get; set; }
        public int StepId { get; set; }
    }

    /// <summary>
    /// This is the minimalist run object that is passed during execution inside the project run object
    /// </summary>
    public class RunObject
    {
        public string RunId { get; set; }
        public int StepId { get; set; }
    }

    #endregion
}
