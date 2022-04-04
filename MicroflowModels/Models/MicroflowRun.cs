namespace MicroflowModels
{
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
    }

    /// <summary>
    /// This is the minimalist run object that is passed during execution inside the workflow run object
    /// </summary>
    public class RunObject
    {
        public string RunId { get; set; }
        public string StepNumber { get; set; }
        public string GlobalKey { get; set; }
        public string PostData { get; set; }
    }
}
