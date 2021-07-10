using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microflow.Models;
using MicroflowModels;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.Helpers
{
    public static class MicroflowHelper
    {
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

        public static DurableHttpRequest CreateMicroflowDurableHttpRequest(this HttpCall httpCall, string instanceId)
        {
            DurableHttpRequest newDurableHttpRequest;
            string baseUrl = $"http://{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}";


            string callback = string.IsNullOrWhiteSpace(httpCall.CallBackAction)
                    ? ""
                    : $"{baseUrl}/api/{httpCall.CallBackAction}/{instanceId}/{httpCall.RowKey}";

            CalculateGlobalKey(httpCall, baseUrl);

            if (!httpCall.IsHttpGet)
            {
                MicroflowPostData postData = new MicroflowPostData()
                {
                    ProjectName = httpCall.PartitionKey,
                    SubOrchestrationId = instanceId,
                    RunId = httpCall.RunId,
                    StepId = httpCall.StepId,
                    StepNumber = Convert.ToInt32(httpCall.RowKey),
                    MainOrchestrationId = httpCall.MainOrchestrationId,
                    CallbackUrl = callback
                };

                string body = JsonSerializer.Serialize(postData);

                newDurableHttpRequest = new DurableHttpRequest(
                    method: HttpMethod.Post,
                    uri: new Uri(httpCall.ParseUrlMicroflowData(instanceId, postData.CallbackUrl)),
                    timeout: TimeSpan.FromSeconds(httpCall.ActionTimeoutSeconds),
                    content: body,
                    asynchronousPatternEnabled: httpCall.AsynchronousPollingEnabled
                //headers: durableHttpRequest.Headers,
                //tokenSource: durableHttpRequest.TokenSource

                );

                // Do not copy over the x-functions-key header, as in many cases, the
                // functions key used for the initial request will be a Function-level key
                // and the status endpoint requires a master key.
                //newDurableHttpRequest.Headers.Remove("x-functions-key");

            }
            else
            {
                newDurableHttpRequest = new DurableHttpRequest(
                    method: HttpMethod.Get,
                    uri: new Uri(httpCall.ParseUrlMicroflowData(instanceId, callback)),
                    timeout: TimeSpan.FromSeconds(httpCall.ActionTimeoutSeconds),
                    asynchronousPatternEnabled: httpCall.AsynchronousPollingEnabled
                //headers: durableHttpRequest.Headers,
                //tokenSource: durableHttpRequest.TokenSource

                );

                // Do not copy over the x-functions-key header, as in many cases, the
                // functions key used for the initial request will be a Function-level key
                // and the status endpoint requires a master key.
                //newDurableHttpRequest.Headers.Remove("x-functions-key");
            }

            return newDurableHttpRequest;
        }


        /// <summary>
        /// Work out what the global key is for this call
        /// </summary>
        private static void CalculateGlobalKey(HttpCall httpCall, string baseUrl)
        {
            // check if it is call to Microflow
            if (httpCall.CalloutUrl.StartsWith($"{baseUrl}/api/start/"))
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

        public static string ParseUrlMicroflowData(this HttpCall httpCall, string instanceId, string callbackUrl)
        {
            StringBuilder sb = new StringBuilder(httpCall.CalloutUrl);

            sb.Replace("<ProjectName>", httpCall.PartitionKey);
            sb.Replace("<MainOrchestrationId>", httpCall.MainOrchestrationId);
            sb.Replace("<SubOrchestrationId>", instanceId);
            sb.Replace("<CallbackUrl>", callbackUrl);
            sb.Replace("<RunId>", httpCall.RunId);
            sb.Replace("<StepId>", httpCall.StepId);
            sb.Replace("<StepNumber>", httpCall.RowKey);

            return sb.ToString();
        }
    }
}
