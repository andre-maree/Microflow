﻿using System;
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

        public static async Task<HttpResponseMessage> LogError(string projectName, Exception e)
        {
            await new LogErrorEntity(projectName, -999, e.Message).LogError();

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(e.Message)
            };

            return resp;
        }

        public static DurableHttpRequest CreateMicroflowDurableHttpRequest(this HttpCall httpCall, string instanceId)
        {
            DurableHttpRequest newDurableHttpRequest;

            string callback = string.IsNullOrWhiteSpace(httpCall.CallBackAction)
                    ? ""
                    : $"{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}/api/{httpCall.CallBackAction}/{instanceId}/{httpCall.RowKey}";

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
                    asynchronousPatternEnabled: httpCall.AsyncronousPollingEnabled
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
                    asynchronousPatternEnabled: httpCall.AsyncronousPollingEnabled
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

        public static string ParseUrlMicroflowData(this HttpCall httpCall, string instanceId, string callbackUrl)
        {
            //string baseUrl = $"http://{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}";

            //if (httpCall.CalloutUrl.StartsWith($"{baseUrl}/api/start/"))
            //{
            //    httpCall.CalloutUrl = baseUrl + "/api/callmicroflow/" + Uri.EscapeDataString(httpCall.CalloutUrl);
            //}

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
