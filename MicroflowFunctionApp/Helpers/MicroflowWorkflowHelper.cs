using System;
using System.Collections.Specialized;
using System.Net.Http;
using MicroflowModels;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using static MicroflowModels.Constants;

namespace Microflow.Helpers
{
    public static class MicroflowWorkflowHelper
    {
        public static MicroflowRun CreateMicroflowRun(HttpRequestMessage req, ref string instanceId, string workflowName)
        {
            return MicroflowStartupHelper.CreateStartupRun(req.RequestUri.ParseQueryString(), ref instanceId, workflowName);
        }

        public static Microsoft.Azure.WebJobs.Extensions.DurableTask.RetryOptions GetRetryOptions(this IHttpCallWithRetries httpCallWithRetries)
        {
            Microsoft.Azure.WebJobs.Extensions.DurableTask.RetryOptions ops = new(TimeSpan.FromSeconds(httpCallWithRetries.RetryDelaySeconds),
                                                httpCallWithRetries.RetryMaxRetries + 1)
            {
                RetryTimeout = TimeSpan.FromSeconds(httpCallWithRetries.RetryTimeoutSeconds),
                MaxRetryInterval = TimeSpan.FromSeconds(httpCallWithRetries.RetryMaxDelaySeconds),
                BackoffCoefficient = httpCallWithRetries.RetryBackoffCoefficient
            };

            return ops;
        }

        /// <summary>
        /// Work out what the global key is for this call
        /// </summary>
        [Deterministic]
        public static void CalculateGlobalKey(this HttpCall httpCall)
        {
            // check if it is call to Microflow
            if (httpCall.CalloutUrl.StartsWith($"{CallNames.BaseUrl}start/"))
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
    }
}
