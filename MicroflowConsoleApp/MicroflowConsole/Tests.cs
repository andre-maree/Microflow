using MicroflowModels;
using MicroflowSDK;
using System.Collections.Generic;

namespace MicroflowConsole
{
    public static class Tests
    {
        public static List<Step> CreateTestWorkflow_SimpleSteps()
        {
            // create
            List<Step> steps = ProjectManager.CreateSteps(4, 1,"{default_post_url}");

            steps[0].AddSubSteps(steps[1].StepId, steps[2].StepId);

            steps[3].AddParentSteps(steps[1], steps[2]);

            //steps[0].CallbackAction = "approve";
            //steps[0].StopOnActionFailed = true;
            //steps[0].ActionTimeoutSeconds = 30;

            //StepsManager.SetRetryForSteps(5, 10, 2, 30, 5, steps.Values.ToArray());
            //StepsManager.SetRetryForSteps(5, 10, 2, 30, 5, steps[2]);
            //steps[1].CalloutUrl = "";// this now acts as a container for a list of top level steps
            return steps;
        }

        public static List<Step> CreateTestWorkflow_10StepsParallel()
        {
            List<Step> steps = ProjectManager.CreateSteps(14, 1, "{default_post_url}");

            steps[0].AddSubSteps(steps[1].StepId, steps[2].StepId);

            steps[1].AddSubSteps(steps[3].StepId, steps[4].StepId, steps[5].StepId, steps[6].StepId, steps[7].StepId);

            steps[2].AddSubSteps(steps[8].StepId, steps[9].StepId, steps[10].StepId, steps[11].StepId, steps[12].StepId);

            // 2 groups of 5 parallel steps = 10 parallel steps
            steps[13].AddParentSteps(steps[3], steps[4], steps[5], steps[6], steps[7], steps[8], steps[9], steps[10], steps[11], steps[12]);

            // step configs
            //steps[1].CallbackAction = "approve_process_start";
            //steps[14].CallbackAction = "approve_process_end";

            return steps;
        }

        public static List<Step> CreateTestWorkflow_Complex1()
        {
            List<Step> steps = ProjectManager.CreateSteps(8, 1, "{default_post_url}");

            steps[0].AddSubSteps(steps[1].StepId, steps[2].StepId, steps[3].StepId);

            steps[3].AddSubSteps(steps[4].StepId, steps[5].StepId);

            steps[2].AddSubSteps(steps[5].StepId, steps[6].StepId);

            steps[3].AddSubSteps(steps[6].StepId, steps[7].StepId);

            steps[4].AddSubSteps(steps[5].StepId, steps[2].StepId);

            steps[5].AddSubSteps(steps[7].StepId);

            steps[6].AddSubSteps(steps[7].StepId);

            //steps[7].CallbackAction = "approve";

            return steps;
        }
    }
}
