using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microflow.Models;
using MicroflowModels;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.Helpers
{
    public static class MicroflowHelper
    {
        public static RetryOptions GetRetryOptions(this HttpCallWithRetries httpCallWithRetries)
        {
            RetryOptions ops = new RetryOptions(TimeSpan.FromSeconds(httpCallWithRetries.RetryDelaySeconds), httpCallWithRetries.RetryMaxRetries);
            ops.RetryTimeout = TimeSpan.FromSeconds(httpCallWithRetries.RetryTimeoutSeconds);
            ops.MaxRetryInterval = TimeSpan.FromSeconds(httpCallWithRetries.RetryMaxDelaySeconds);
            ops.BackoffCoefficient = httpCallWithRetries.RetryBackoffCoefficient;

            return ops;
        }

        public static async Task<HttpResponseMessage> LogError(string projectName, Exception e)
        {
            await new LogErrorEntity(projectName, e.Message).LogError();

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(e.Message)
            };

            return resp;
        }

        public static DurableHttpRequest CreateMicroflowDurableHttpRequest(this HttpCall httpCall, string instanceId)
        {
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
                    StepId = httpCall.RowKey,
                    MainOrchestrationId = httpCall.MainOrchestrationId,
                    CallbackUrl = callback
                };

                string body = JsonSerializer.Serialize(postData);

                DurableHttpRequest newDurableHttpRequest = new DurableHttpRequest(
                    method: HttpMethod.Post,
                    uri: new Uri(httpCall.ParseUrlMicroflowData(instanceId, postData.CallbackUrl)),
                    timeout: TimeSpan.FromSeconds(httpCall.ActionTimeoutSeconds),
                    //headers: durableHttpRequest.Headers,
                    content: body
                    //tokenSource: durableHttpRequest.TokenSource
                    
                );

                // Do not copy over the x-functions-key header, as in many cases, the
                // functions key used for the initial request will be a Function-level key
                // and the status endpoint requires a master key.
                //newDurableHttpRequest.Headers.Remove("x-functions-key");

                return newDurableHttpRequest;
            }
            else
            {
                DurableHttpRequest newDurableHttpRequest = new DurableHttpRequest(
                    method: HttpMethod.Get,
                    uri: new Uri(httpCall.ParseUrlMicroflowData(instanceId, callback)),
                    timeout: TimeSpan.FromSeconds(httpCall.ActionTimeoutSeconds)
                //headers: durableHttpRequest.Headers,
                              //tokenSource: durableHttpRequest.TokenSource

                );

                // Do not copy over the x-functions-key header, as in many cases, the
                // functions key used for the initial request will be a Function-level key
                // and the status endpoint requires a master key.
                //newDurableHttpRequest.Headers.Remove("x-functions-key");

                return newDurableHttpRequest;
            }
        }
    }
}
