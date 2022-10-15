using System.Collections.Generic;

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
        public MicroflowHttpResponse MicroflowStepResponseData { get; set; }
    }

    /// <summary>
    /// Used to hold Microflow specific http status code results
    /// </summary>
    public class MicroflowHttpResponseBase
    {
        public bool Success { get; set; }
        public int HttpResponseStatusCode { get; set; }
        public string Content { get; set; }
    }

    public class MicroflowHttpResponse : MicroflowHttpResponseBase//, IMicroflowHttpResponse
    {
        public List<int> SubStepsToRun { get; set; }
        public string Action { get; set; }
        public CalloutOrWebhook CalloutOrWebhook { get;set;}
    }
    //public interface IMicroflowHttpResponse
    //{
    //    int HttpResponseStatusCode { get; set; }
    //    string Content { get; set; }
    //    bool Success { get; set; }
    //    public List<int> SubStepsToRun { get; set; }
    //}
}
