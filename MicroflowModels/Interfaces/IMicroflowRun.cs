namespace MicroflowModels
{
    public interface IMicroflowRun
    {
        int CurrentLoop { get; set; }
        int Loop { get; set; }
        string OrchestratorInstanceId { get; set; }
        int PausedStepId { get; set; }
        string WorkflowName { get; set; }
        RunObject RunObject { get; set; }
    }
}