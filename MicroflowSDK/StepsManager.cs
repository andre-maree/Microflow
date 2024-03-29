﻿using MicroflowModels;
using System.Collections.Generic;
using System.Linq;

namespace MicroflowSDK
{
    public static class StepsManager
    {
        public static void AddParentSteps(this Step step, params Step[] parents)
        {
            foreach (Step parentStep in parents)
            {
                parentStep.SubSteps.Add(step.StepNumber);
            }
        }

        public static void AddSubSteps(this Step step, params Step[] subSteps)
        {
            foreach (Step subStep in subSteps)
            {
                step.SubSteps.Add(subStep.StepNumber);
            }
        }

        public static void AddSubStepRange(this Step step, List<Step> steps, int fromId, int toId)
        {
            List<Step> li = steps.FindAll(s => s.StepNumber >= fromId && s.StepNumber <= toId);
            step.SubSteps.AddRange(from s in li select s.StepNumber);
        }

        public static void SetRetryForSteps(params Step[] steps)
        {
            foreach (Step step in steps)
            {
                step.RetryOptions = new RetryOptions();
            }
        }

        public static void SetRetryForSteps(int delaySeconds, int maxDelaySeconds, int maxRetries, int timeOutSeconds, int backoffCoefficient, params Step[] steps)
        {
            RetryOptions retryOptions = new RetryOptions()
            {
                DelaySeconds = delaySeconds,
                MaxDelaySeconds = maxDelaySeconds,
                MaxRetries = maxRetries,
                TimeOutSeconds = timeOutSeconds,
                BackoffCoefficient = backoffCoefficient
            };

            foreach(Step step in steps)
            {
                step.RetryOptions = retryOptions;
            }
        }
    }
}
