namespace Microflow.Models
{
    public interface IHttpCall : IStepEntity
    {
        int ActionTimeoutSeconds { get; set; }
        string CallBackAction { get; set; }
        bool IsHttpGet { get; set; }
        string MainOrchestrationId { get; set; }
        string RunId { get; set; }
        bool StopOnActionFailed { get; set; }
        string CalloutUrl { get; set; }
    }
}