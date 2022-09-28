using MicroflowModels;
using System.Collections.Generic;
using System.Linq;

namespace MicroflowSDK
{
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

        public static Dictionary<string, string> CreateMergeFields()
        {
            string querystring = "?ProjectName=<ProjectName>&MainOrchestrationId=<MainOrchestrationId>&SubOrchestrationId=<SubOrchestrationId>&CallbackUrl=<CallbackUrl>&RunId=<RunId>&StepNumber=<StepNumber>&GlobalKey=<GlobalKey>";// &StepId=<StepId>";

            Dictionary<string, string> mergeFields = new();
            // use 
            mergeFields.Add("default_post_url", "https://reqbin.com/echo/post/json" + querystring);
            // set the callout url to the new SleepTestOrchestrator http normal function url
            //mergeFields.Add("default_post_url", baseUrl + "/SleepTestOrchestrator_HttpStart" + querystring);
            //mergeFields.Add("default_post_url", baseUrl + "/testpost" + querystring);

            return mergeFields;
        }
    }
}
