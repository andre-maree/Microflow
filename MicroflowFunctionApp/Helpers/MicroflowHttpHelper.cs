﻿using MicroflowModels;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Primitives;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using static MicroflowModels.Constants;

namespace Microflow.Helpers
{
    public static class MicroflowHttpHelper
    {
        /// <summary>
        /// Used to handle callout responses
        /// </summary>
        [Deterministic]
        public static MicroflowHttpResponse GetMicroflowResponse(this DurableHttpResponse durableHttpResponse, bool forwardPostData)
        {
            int statusCode = (int)durableHttpResponse.StatusCode;

            if (statusCode <= 200 || ((statusCode > 201) && (statusCode < 300)))
            {
                return new MicroflowHttpResponse() { Success = true, HttpResponseStatusCode = statusCode, Content = forwardPostData ? durableHttpResponse.Content : string.Empty };
            }

            // if 201 created try get the location header to save it in the steps log
            if (statusCode != 201)
                return new MicroflowHttpResponse() { Success = false, HttpResponseStatusCode = statusCode };

            return durableHttpResponse.Headers.TryGetValue("location", out StringValues values)
                ? new MicroflowHttpResponse() { Success = true, HttpResponseStatusCode = statusCode, Content = values[0] }
                : new MicroflowHttpResponse() { Success = true, HttpResponseStatusCode = statusCode };
        }

        [Deterministic]
        public static DurableHttpRequest CreateMicroflowDurableHttpRequest(this HttpCall httpCall, string instanceId, MicroflowHttpResponse microflowHttpResponse)
        {
            DurableHttpRequest newDurableHttpRequest;

            //string webhook = string.IsNullOrWhiteSpace(httpCall.WebhookId)
            //        ? ""
            //        : httpCall.WebhookId;

            httpCall.CalculateGlobalKey();

            if (!httpCall.IsHttpGet)
            {
                MicroflowPostData postData = new()
                {
                    WorkflowName = httpCall.PartitionKey,
                    SubOrchestrationId = instanceId,
                    RunId = httpCall.RunId,
                    StepId = httpCall.StepId,
                    StepNumber = Convert.ToInt32(httpCall.RowKey),
                    MainOrchestrationId = httpCall.MainOrchestrationId,
                    Webhook = httpCall.EnableWebhook ? $"{CallNames.BaseUrl}/webhooks/{httpCall.WebhookId}" : null,
                    GlobalKey = httpCall.GlobalKey,
                    PostData = microflowHttpResponse.Content
                };

                string body = JsonSerializer.Serialize(postData);

                newDurableHttpRequest = new DurableHttpRequest(
                    method: HttpMethod.Post,
                    uri: new Uri(httpCall.CalloutUrl),
                    timeout: TimeSpan.FromSeconds(httpCall.CalloutTimeoutSeconds),
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
                    uri: new Uri(httpCall.CalloutUrl),
                    timeout: TimeSpan.FromSeconds(httpCall.CalloutTimeoutSeconds),
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

        [Deterministic]
        public static void ParseUrlMicroflowData(IHttpCallWithRetries httpCall, string instanceId)
        {
            // do not replace the querystring if it is a post
            if(!httpCall.IsHttpGet)
            {
                return;
            }

            StringBuilder sb = new(httpCall.CalloutUrl);

            sb.Replace("<WorkflowName>", httpCall.PartitionKey);
            sb.Replace("<MainOrchestrationId>", httpCall.MainOrchestrationId);
            sb.Replace("<SubOrchestrationId>", instanceId);
            if(httpCall.EnableWebhook)
            {
                sb.Replace("<WebhookId>", $"{CallNames.BaseUrl}/webhooks/{httpCall.WebhookId}");
            }
            sb.Replace("<RunId>", httpCall.RunId);
            sb.Replace("<StepId>", httpCall.StepId);
            sb.Replace("<StepNumber>", httpCall.RowKey);
            sb.Replace("<GlobalKey>", httpCall.GlobalKey);

            httpCall.CalloutUrl = sb.ToString();
        }
    }
}