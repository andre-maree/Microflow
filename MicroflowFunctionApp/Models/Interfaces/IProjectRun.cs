namespace Microflow.Models
{
    public interface IProjectRun
    {
        int CurrentLoop { get; set; }
        int Loop { get; set; }
        string OrchestratorInstanceId { get; set; }
        int PausedStepId { get; set; }
        string ProjectName { get; set; }
        string BaseUrl { get; set; }
        RunObject RunObject { get; set; }
    }
}