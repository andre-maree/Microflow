using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microflow.Models;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.Helpers
{
    public static class MicroflowEntities
    {
        public const string StepCounter = "StepCounter";
        public const string CanExecuteNowCounter = "CanExecuteNowCounter";
    }

    public static class MicroflowCounterKeys
    {
        public const string Add = "add";
        public const string Subtract = "subtract";
        public const string Read = "get";
    }

    public static class MicroflowStateKeys
    {
        public const string ProjectStateId = "ProjectState";
        public const string GlobalStateId = "GlobalState";
    }

    public static class MicroflowControlKeys
    {
        public const string Ready = "ready";
        public const string Pause = "pause";
        public const string Stop = "stop";
        public const string Read = "get";
    }

    public static class MicroflowHelper
    {
        /// <summary>
        /// Work out what the global key is for this call
        /// </summary>
        [Deterministic]
        public static void CalculateGlobalKey(this HttpCall httpCall)
        {
            // check if it is call to Microflow
            if (httpCall.CalloutUrl.StartsWith($"{httpCall.BaseUrl}/api/start/"))
            {
                // parse query string
                NameValueCollection data = new Uri(httpCall.CalloutUrl).ParseQueryString();
                // if there is query string data
                if (data.Count > 0)
                {
                    // check if there is a global key (maybe if it is an assigned key)
                    if (string.IsNullOrEmpty(data.Get("globalkey")))
                    {
                        httpCall.CalloutUrl += $"&globalkey={httpCall.GlobalKey}";
                    }
                }
                else
                {
                    httpCall.CalloutUrl += $"?globalkey={httpCall.GlobalKey}";
                }
            }
        }

        public static RetryOptions GetRetryOptions(this IHttpCallWithRetries httpCallWithRetries)
        {
            RetryOptions ops = new RetryOptions(TimeSpan.FromSeconds(httpCallWithRetries.RetryDelaySeconds), httpCallWithRetries.RetryMaxRetries);
            ops.RetryTimeout = TimeSpan.FromSeconds(httpCallWithRetries.RetryTimeoutSeconds);
            ops.MaxRetryInterval = TimeSpan.FromSeconds(httpCallWithRetries.RetryMaxDelaySeconds);
            ops.BackoffCoefficient = httpCallWithRetries.RetryBackoffCoefficient;

            return ops;
        }

        public static async Task<HttpResponseMessage> LogError(string projectName, string globalKey, string runId, Exception e)
        {
            await new LogErrorEntity(projectName, -999, e.Message, globalKey, runId).LogError();

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(e.Message)
            };

            return resp;
        }
    }
}
