﻿using MicroflowModels;
using System.Collections.Generic;

namespace MicroflowSDK
{
    public static class StepsManager
    {
        public static void AddParentSteps(this Step step, params Step[] parents)
        {
            foreach (var parentStep in parents)
            {
                parentStep.SubSteps.Add(step.StepId);
            }
        }

        public static void AddSubSteps(this Step step, params int[] subSteps)
        {
            foreach (var subStep in subSteps)
            {
                step.SubSteps.Add(subStep);
            }
        }

        public static void SetRetryForStep(this Step step, int delaySeconds = 5, int maxDelaySeconds = 60, int maxRetries = 5, int timeOutSeconds = 30, int backoffCoefficient = 1)
        {
            var retryOptions = new MicroflowRetryOptions()
            {
                DelaySeconds = delaySeconds,
                MaxDelaySeconds = maxDelaySeconds,
                MaxRetries = maxRetries,
                TimeOutSeconds = timeOutSeconds,
                BackoffCoefficient = backoffCoefficient
            };

            step.RetryOptions = retryOptions;
        }

        public static void SetRetryForSteps(int delaySeconds = 5, int maxDelaySeconds = 60, int maxRetries = 5, int timeOutSeconds = 30, int backoffCoefficient = 1, params Step[] steps)
        {
            var retryOptions = new MicroflowRetryOptions()
            {
                DelaySeconds = delaySeconds,
                MaxDelaySeconds = maxDelaySeconds,
                MaxRetries = maxRetries,
                TimeOutSeconds = timeOutSeconds,
                BackoffCoefficient = backoffCoefficient
            };

            foreach(var step in steps)
            {
                step.RetryOptions = retryOptions;
            }
        }
    }
}