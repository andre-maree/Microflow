using MicroflowModels;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MicroflowSDK
{
    public class PassThroughParams
    {
        public bool WorkflowName { get; set; } = true;
        public bool MainOrchestrationId { get; set; } = true;
        public bool SubOrchestrationId { get; set; } = true;
        public bool Webhook { get; set; } = true;
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

        public static Dictionary<string, string> CreateMergeFields(PassThroughParams passThroughParams)
        {
            PropertyInfo[] props = passThroughParams.GetType().GetProperties();
            string querystring = "?";
            foreach (var param in props)
            {
                var val = param.GetValue(passThroughParams);
                if ((bool)val == true)
                {
                    querystring += $"{param.Name}=<{param.Name}>&";
                }
            }
            querystring = querystring.Remove(querystring.Length - 1);
            //string querystring2 = "?WorkflowName=<WorkflowName>&MainOrchestrationId=<MainOrchestrationId>&SubOrchestrationId=<SubOrchestrationId>&Webhook=<Webhook>&RunId=<RunId>&StepNumber=<StepNumber>&GlobalKey=<GlobalKey>&StepId=<StepId>";

            Dictionary<string, string> mergeFields = new();
            // use 
            mergeFields.Add("default_post_url", "https://reqbin.com/echo/post/json");// + querystring);
            // set the callout url to the new SleepTestOrchestrator http normal function url
            //mergeFields.Add("default_post_url", baseUrl + "/SleepTestOrchestrator_HttpStart" + querystring);
            //mergeFields.Add("default_post_url", baseUrl + "/testpost" + querystring);

            return mergeFields;
        }
    }
}
