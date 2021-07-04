﻿using MicroflowModels;
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
            //steps[0].IsHttpGet = true;
            //steps[0].ActionTimeoutSeconds = 1;
            steps[0].CallbackAction = "approve";
            //steps[0].CalloutUrl = "http://localhost:7071/api/SleepTestOrchestrator_HttpStart";
            //steps[0].SetRetryForStep(1, 2,1);
            //steps[0].StopOnActionFailed = true;

            steps[0].AddSubSteps(steps[1].StepNumber, steps[2].StepNumber);

            steps[3].AddParentSteps(steps[1], steps[2]);

            //steps[0].CallbackAction = "approve"; 
            //steps[1].CallbackAction = "approve"; 
            //steps[2].CallbackAction = "approve"; 
            //steps[3].CallbackAction = "approve";
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

            steps[0].AddSubSteps(steps[1].StepNumber, steps[2].StepNumber);

            steps[1].AddSubSteps(steps[3].StepNumber, steps[4].StepNumber, steps[5].StepNumber, steps[6].StepNumber, steps[7].StepNumber);

            steps[2].AddSubSteps(steps[8].StepNumber, steps[9].StepNumber, steps[10].StepNumber, steps[11].StepNumber, steps[12].StepNumber);

            // 2 groups of 5 parallel steps = 10 parallel steps
            steps[13].AddParentSteps(steps[3], steps[4], steps[5], steps[6], steps[7], steps[8], steps[9], steps[10], steps[11], steps[12]);

            // step configs
            //steps[2].CallbackAction = "approve_process_start";
            //steps[13].CallbackAction = "approve_process_end";

            return steps;
        }

        public static List<Step> CreateTestWorkflow_Complex1()
        {
            List<Step> steps = ProjectManager.CreateSteps(8, 1, "{default_post_url}");

            steps[0].AddSubSteps(steps[1].StepNumber, steps[2].StepNumber, steps[3].StepNumber);

            steps[3].AddSubSteps(steps[4].StepNumber, steps[5].StepNumber);

            steps[2].AddSubSteps(steps[5].StepNumber, steps[6].StepNumber);

            steps[3].AddSubSteps(steps[6].StepNumber, steps[7].StepNumber);

            steps[4].AddSubSteps(steps[5].StepNumber, steps[2].StepNumber);

            steps[5].AddSubSteps(steps[7].StepNumber);

            steps[6].AddSubSteps(steps[7].StepNumber);

            //steps[7].CallbackAction = "approve";

            return steps;
        }


        public static List<Step> CreateTestWorkflow_110Steps()
        {
            List<Step> steps = ProjectManager.CreateSteps(110, 1, "{default_post_url}");

            steps[0].AddSubStepRange(steps, 2, 11);
            steps[11].AddSubStepRange(steps, 13, 22);

            steps[22].AddSubStepRange(steps, 24, 33);
            steps[33].AddSubStepRange(steps, 35, 44);
            steps[44].AddSubStepRange(steps, 46, 55);
            steps[55].AddSubStepRange(steps, 57, 66);
            steps[66].AddSubStepRange(steps, 68, 77);
            steps[77].AddSubStepRange(steps, 79, 88);
            steps[88].AddSubStepRange(steps, 90, 99);
            steps[99].AddSubStepRange(steps, 101, 110);
            //foreach(var step in steps)
            //{

            //}

            //steps[7].CallbackAction = "approve";

            return steps;
        }
    }
}
