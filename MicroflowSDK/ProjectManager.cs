using MicroflowModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroflowSDK
{
    public static class ProjectManager
    {
        public static Step Step(this Microflow microFlow, int stepNumber) => microFlow.Steps.First(s=>s.StepNumber == stepNumber);
        public static List<Step>  CreateSteps(int count, int fromId, string defaultURI = "")
        {
            List<Step> stepsList = new List<Step>();
            stepsList.Add(new Step()); // placeholder

            for (; fromId <= count; fromId++)
            {
                Step step = new Step(fromId, defaultURI, "myStep " + fromId);
                stepsList.Add(step);
            }

            return stepsList;
        }
    }
}
