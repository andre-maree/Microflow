﻿using System;

namespace MicroflowModels.Constants
{
    public static class Constants
    {

#if !DEBUG_NOUPSERT_NOFLOWCONTROL_NOSCALEGROUPS && !DEBUG_NOUPSERT_NOSCALEGROUPS
        public static class ScaleGroupCalls
        {
            public const string CanExecuteNowInScaleGroup = "CanExecuteNowInScaleGroup";
            public const string CanExecuteNowInScaleGroupCount = "CanExecuteNowInScaleGroupCount";
            public const string ScaleGroupMaxConcurrentInstanceCount = "ScaleGroupMaxConcurrentInstanceCount";
        }
#endif

        public static class CallNames
        {
            public static readonly string CallbackBase = $"{Environment.GetEnvironmentVariable("CallbackBase")}";
            public const string CanExecuteNow = "CanExecuteNow";
            public const string ExecuteStep = "ExecuteStep";
            public const string GetStep = "GetStep";
            public const string LogError = "LogError";
            public const string LogStep = "LogStep";
            public const string LogOrchestration = "LogOrchestration";
        }

        public static class MicroflowStates
        {
            public const int Ready = 0;
            public const int Paused = 1;
            public const int Stopped = 2;
        }

        public static class MicroflowEntities
        {
            public const string StepCount = "StepCount";
            public const string CanExecuteNowCount = "CanExecuteNowCount";
        }

        public static class MicroflowCounterKeys
        {
            public const string Add = "add";
            public const string Subtract = "subtract";
            public const string Read = "get";
            public const string Delete = "delete";
            public const string Set = "set";
        }

        public static class MicroflowStateKeys
        {
            public const string WorkflowState = "WorkflowState";
            public const string GlobalState = "GlobalState";
        }

        public static class MicroflowControlKeys
        {
            public const string Ready = "ready";
            public const string Pause = "pause";
            public const string Stop = "stop";
            public const string Read = "get";
        }

        public static readonly char[] Splitter = { ',', ';' };
}
}