using System;

namespace MicroflowModels
{
    public static class Constants
    {

#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_SCALEGROUPS && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_SCALEGROUPS && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT
        public static class ScaleGroupCalls
        {
            public const string CanExecuteNowInScaleGroup = "CanExecuteNowInScaleGroup";
            public const string CanExecuteNowInScaleGroupCount = "CanExecuteNowInScaleGroupCount";
            public const string ScaleGroupMaxConcurrentInstanceCount = "ScaleGroupMaxConcurrentInstanceCount";
        }
#endif
        public static class PollingConfig
        {
            public static readonly long PollingMaxHours = Convert.ToInt64(Environment.GetEnvironmentVariable("PollingMaxHours"));
            public static readonly int PollingIntervalMaxSeconds = Convert.ToInt32(Environment.GetEnvironmentVariable("PollingIntervalMaxSeconds"));
            public static readonly int PollingIntervalSeconds = Convert.ToInt32(Environment.GetEnvironmentVariable("PollingIntervalSeconds"));
        }

        public const string MicroflowBase = "microflow/v1";

        public static class CallNames
        {
            public static readonly string BaseUrl = $"{Environment.GetEnvironmentVariable("BasePrefix")}{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}/{MicroflowBase}";
            public static readonly string CallbackUrl = $"{BaseUrl}/Webhook";
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
