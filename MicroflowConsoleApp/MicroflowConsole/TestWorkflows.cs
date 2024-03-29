﻿using MicroflowModels;
using MicroflowSDK;
using System.Collections.Generic;

namespace MicroflowConsole
{
    public static class TestWorkflows
    {
        public static List<Step> CreateTestWorkflow_SimpleSteps()
        {
            // create
            List<Step> steps = WorkflowManager.CreateSteps(4, 1,"{default_post_url}");
            //steps[0].IsHttpGet = true;
            //steps[0].CallbackTimeoutSeconds = 30;
            //steps[0].WebhookAction = "approve";
            //steps[0].CalloutUrl = "http://localhost:7071/SleepTestOrchestrator_HttpStart";
            //steps[0].SetRetryForStep(1, 2, 1);
            //steps[0].StopOnActionFailed = true;
            //steps[0].CalloutTimeoutSeconds = 190;

            steps[1].AddSubSteps(steps[2], steps[3]);
            steps[4].AddParentSteps(steps[2], steps[3]);

            steps.Remove(steps[0]);

            return steps;
        }

        public static List<Step> CreateTestWorkflow_10StepsParallel()
        {
            List<Step> steps = WorkflowManager.CreateSteps(14, 1, "{default_post_url}");

            steps[1].AddSubSteps(steps[2], steps[3]);
            steps[2].AddSubSteps(steps[4], steps[5], steps[6], steps[7], steps[8]);
            steps[3].AddSubSteps(steps[9], steps[10], steps[11], steps[12], steps[13]);
            // 2 groups of 5 parallel steps = 10 parallel steps
            steps[14].AddParentSteps(steps[4], steps[5], steps[6], steps[7], steps[8], steps[9], steps[10], steps[11], steps[12], steps[13]);

            steps.Remove(steps[0]);

            return steps;
        }

        public static List<Step> CreateTestWorkflow_Complex1()
        {
            List<Step> steps = WorkflowManager.CreateSteps(8, 1, "{default_post_url}");

            steps[1].AddSubSteps(steps[2], steps[3], steps[4]);
            steps[2].AddSubSteps(steps[5], steps[6]);
            steps[3].AddSubSteps(steps[6], steps[7]);
            steps[4].AddSubSteps(steps[6], steps[8]);
            steps[5].AddSubSteps(steps[3], steps[6]);
            steps[6].AddSubSteps(steps[8]);
            steps[7].AddSubSteps(steps[8]);

            steps.Remove(steps[0]);

            return steps;
        }


        public static List<Step> CreateTestWorkflow_110Steps()
        {
            List<Step> steps = WorkflowManager.CreateSteps(110, 1, "{default_post_url}");

            steps[1].AddSubStepRange(steps, 3, 11);
            steps[11].AddSubStepRange(steps, 13, 22);
            steps[22].AddSubStepRange(steps, 24, 33);
            steps[33].AddSubStepRange(steps, 35, 44);
            steps[44].AddSubStepRange(steps, 46, 55);
            steps[55].AddSubStepRange(steps, 57, 66);
            steps[66].AddSubStepRange(steps, 68, 77);
            steps[77].AddSubStepRange(steps, 79, 88);
            steps[88].AddSubStepRange(steps, 90, 99);
            steps[99].AddSubStepRange(steps, 101, 110);

            steps.Remove(steps[0]);

            return steps;
        }
    }
}
