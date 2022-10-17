using MicroflowModels;
using System.Collections.Generic;
using System.Linq;

namespace MicroflowSDK
{
    public class PassThroughParams
    {
        public bool WorkflowName { get; set; } = true;
        public bool MainOrchestrationId { get; set; } = true;
        public bool SubOrchestrationId { get; set; } = true;
        public bool WebhookId { get; set; } = true;
        public bool RunId { get; set; } = true;
        public bool StepNumber { get; set; } = true;
        public bool GlobalKey { get; set; } = true;
        public bool StepId { get; set; } = true;
    }

    public static class WorkflowManager
    {
        public static Step Step(this Microflow microFlow, int stepNumber) => microFlow.Steps.First(s=>s.StepNumber == stepNumber);

        public static Step StepNumber(this List<Step> steps, int stepNumber) => steps.First(s => s.StepNumber == stepNumber);

        public static List<Step> CreateSteps(int count, int fromId, string defaultCalloutURI = "")
        {
            List<Step> stepsList = new();

            for (; fromId <= count; fromId++)
            {
                Step step = new(fromId, defaultCalloutURI, "myStep " + fromId);
                stepsList.Add(step);
            }

            return stepsList;
        }
    }
}
