using System;
using System.Net.Http;
using MicroflowModels;

namespace Microflow.Helpers
{
    public static class MicroflowWorkflowHelper
    {
        public static MicroflowRun CreateMicroflowRun(HttpRequestMessage req, ref string instanceId, string workflowName)
        {
            MicroflowRun workflowRun = MicroflowStartupHelper.CreateStartupRun(req.RequestUri.ParseQueryString(), ref instanceId, workflowName);
            string baseUrl = $"{Environment.GetEnvironmentVariable("BaseUrl")}";
            workflowRun.BaseUrl = baseUrl.EndsWith('/')
                ? baseUrl.Remove(baseUrl.Length - 1)
                : baseUrl;

            return workflowRun;
        }
    }
}
