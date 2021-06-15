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
        public static Dictionary<int, Step> CreateSteps(int count, int fromId, string defaultURI = "")
        {
            Dictionary<int, Step> stepsList = new Dictionary<int, Step>();

            for (; fromId <= count; fromId++)
            {
                Step step = new Step(fromId, defaultURI);
                stepsList.Add(fromId, step);
            }

            return stepsList;
        }
    }
}
