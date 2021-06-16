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
        public static List<Step>  CreateSteps(int count, int fromId, string defaultURI = "")
        {
            List<Step> stepsList = new List<Step>();

            for (; fromId <= count; fromId++)
            {
                Step step = new Step(fromId, defaultURI);
                stepsList.Add(step);
            }

            return stepsList;
        }
    }
}
