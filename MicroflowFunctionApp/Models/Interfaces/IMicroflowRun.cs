namespace Microflow.Models
{
    public interface IMicroflowRun
    {
        int CurrentLoop { get; set; }
        int Loop { get; set; }
        string OrchestratorInstanceId { get; set; }
        int PausedStepId { get; set; }
        string WorkflowName { get; set; }
        string BaseUrl { get; set; }
        RunObject RunObject { get; set; }
    }
}