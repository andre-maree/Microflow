//using Microflow;
using MicroflowModels;
using MicroflowSDK;
using System.Collections.Generic;
using System.Linq;

namespace MicroflowConsole
{
    public static class Tests
    {
        public static Step CreateTestWorkflow_SimpleSteps()
        {
            // create
            Dictionary<int, Step> steps = ProjectManager.CreateSteps(4, 1,"{default_post_url}");

            steps[1].AddSubSteps(steps[2], steps[3]);

            steps[4].AddParentSteps(steps[2], steps[3]);

            //steps[1].CallbackAction = "approve";
            //steps[1].StopOnActionFailed = true;
            //steps[1].ActionTimeoutMinutes = 1;

            //StepsManager.SetRetryForSteps(5, 10, 2, 30, 5, steps.Values.ToArray());
            //StepsManager.SetRetryForSteps(5, 10, 2, 30, 5, steps[1], steps[2]);
            //steps[1].CalloutUrl = "";// this now acts as a container for a list of top level steps
            return steps[1];
        }

        public static Step CreateTestWorkflow_10StepsParallel()
        {
            Dictionary<int, Step> steps = ProjectManager.CreateSteps(14, 1,"{default_post_url}");

            steps[1].AddSubSteps(steps[2], steps[3]);

            steps[2].AddSubSteps(steps[4], steps[5], steps[6], steps[7], steps[8]);

            steps[3].AddSubSteps(steps[9], steps[10], steps[11], steps[12], steps[13]);

            // 2 groups of 5 parallel steps = 10 parallel steps
            steps[14].AddParentSteps(steps[4], steps[5], steps[6], steps[7], steps[8], steps[9], steps[10], steps[11], steps[12], steps[13]);

            // step configs
            //steps[1].CallbackAction = "approve_process_start";
            //steps[14].CallbackAction = "approve_process_end";

            return steps[1];
        }

        public static Step CreateTestWorkflow_Complex1()
        {
            Dictionary<int, Step> steps = ProjectManager.CreateSteps(8, 1, "{default_post_url}");

            steps[1].AddSubSteps(steps[2], steps[3], steps[4]);

            steps[2].AddSubSteps(steps[5], steps[6]);

            steps[3].AddSubSteps(steps[6], steps[7]);

            steps[4].AddSubSteps(steps[6], steps[8]);

            steps[5].AddSubSteps(steps[6], steps[3]);

            steps[6].AddSubSteps(steps[8]);

            steps[7].AddSubSteps(steps[8]);

            //steps[7].CallbackAction = "approve";

            return steps[1];
        }
    }
}
