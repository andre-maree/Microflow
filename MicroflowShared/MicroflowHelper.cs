using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace MicroflowShared
{
    public static class MicroflowHelper
    {
        /// <summary>
        /// Pause, run, or stop the workflow, cmd can be "run", "pause", or "stop"
        /// </summary>
        //[FunctionName("Microflow_ProjectControl")]
        //public static async Task<HttpResponseMessage> ProjectControl([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
        //                                                          Route = "ProjectControl/{cmd}/{workflowName}")] HttpRequestMessage req,
        //                                                          [DurableClient] IDurableEntityClient client, string workflowName, string cmd)
        //{
        //    if (cmd.Equals(MicroflowControlKeys.Read, StringComparison.OrdinalIgnoreCase))
        //    {
        //        EntityId projStateId = new EntityId(MicroflowStateKeys.ProjectStateId, workflowName);
        //        EntityStateResponse<string> stateRes = await client.ReadEntityStateAsync<string>(projStateId);

        //        HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK)
        //        {
        //            Content = new StringContent(stateRes.EntityState)
        //        };

        //        return resp;
        //    }

        //    return await client.SetRunState(nameof(ProjectState), workflowName, cmd);
        //}

        /// <summary>
        /// Pause, run, or stop all with the same global key, cmd can be "run", "pause", or "stop"
        /// </summary>
        //[FunctionName("Microflow_GlobalControl")]
        //public static async Task<HttpResponseMessage> GlobalControl([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
        //                                                          Route = "GlobalControl/{cmd}/{globalKey}")] HttpRequestMessage req,
        //                                                          [DurableClient] IDurableEntityClient client, string globalKey, string cmd)
        //{
        //    if (cmd.Equals(MicroflowControlKeys.Read, StringComparison.OrdinalIgnoreCase))
        //    {
        //        EntityId globStateId = new EntityId(MicroflowStateKeys.GlobalStateId, globalKey);
        //        EntityStateResponse<string> stateRes = await client.ReadEntityStateAsync<string>(globStateId);

        //        HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK)
        //        {
        //            Content = new StringContent(stateRes.EntityState)
        //        };

        //        return resp;
        //    }

        //    return await client.SetRunState(nameof(GlobalState), globalKey, cmd);
        //}

        /// <summary>
        /// Durable entity check and set if the global state
        /// </summary>
        //[FunctionName(MicroflowStateKeys.GlobalState)]
        //public static void GlobalState([EntityTrigger] IDurableEntityContext ctx)
        //{
        //    ctx.RunState();
        //}

        /// <summary>
        /// Durable entity check and set workflow state
        /// </summary>
        //[FunctionName(MicroflowStateKeys.WorkflowState)]
        //public static void WorkflowState([EntityTrigger] IDurableEntityContext ctx)
        //{
        //    ctx.RunState();
        //}

        /// <summary>
        /// For workflow and global key states
        /// </summary>
        //private static void RunState(this IDurableEntityContext ctx)
        //{
        //    switch (ctx.OperationName)
        //    {
        //        case MicroflowControlKeys.Ready:
        //            ctx.SetState(MicroflowStates.Ready);
        //            break;
        //        case MicroflowControlKeys.Pause:
        //            ctx.SetState(MicroflowStates.Paused);
        //            break;
        //        case MicroflowControlKeys.Stop:
        //            ctx.SetState(MicroflowStates.Stopped);
        //            break;
        //        case MicroflowControlKeys.Read:
        //            ctx.Return(ctx.GetState<int>());
        //            break;
        //    }
        //}

        /// <summary>
        /// Set the global or workflow state with the key, and the cmd can be "pause", "ready", or "stop"
        /// </summary>
        //public static async Task<HttpResponseMessage> SetRunState(this IDurableEntityClient client,
        //                                                           string stateEntityId,
        //                                                           string key,
        //                                                           string cmd)
        //{
        //    EntityId runStateId = new EntityId(stateEntityId, key);

        //    switch (cmd)
        //    {
        //        case MicroflowControlKeys.Pause:
        //            await client.SignalEntityAsync(runStateId, MicroflowControlKeys.Pause);
        //            break;
        //        case MicroflowControlKeys.Ready:
        //            await client.SignalEntityAsync(runStateId, MicroflowControlKeys.Ready);
        //            break;
        //        case MicroflowControlKeys.Stop:
        //            await client.SignalEntityAsync(runStateId, MicroflowControlKeys.Stop);
        //            break;
        //    }

        //    return new HttpResponseMessage(HttpStatusCode.OK);
        //}

        /// <summary>
        /// Work out what the global key is for this call
        /// </summary>
        //[Deterministic]
        //public static void CalculateGlobalKey(this HttpCall httpCall)
        //{
        //    // check if it is call to Microflow
        //    if (httpCall.CalloutUrl.StartsWith($"{httpCall.BaseUrl}/start/"))
        //    {
        //        // parse query string
        //        NameValueCollection data = new Uri(httpCall.CalloutUrl).ParseQueryString();
        //        // if there is query string data
        //        if (data.Count > 0)
        //        {
        //            // check if there is a global key (maybe if it is an assigned key)
        //            if (string.IsNullOrEmpty(data.Get("globalkey")))
        //            {
        //                httpCall.CalloutUrl += $"&globalkey={httpCall.GlobalKey}";
        //            }
        //        }
        //        else
        //        {
        //            httpCall.CalloutUrl += $"?globalkey={httpCall.GlobalKey}";
        //        }
        //    }
        //}

        //public static RetryOptions GetRetryOptions(this IHttpCallWithRetries httpCallWithRetries)
        //{
        //    RetryOptions ops = new RetryOptions(TimeSpan.FromSeconds(httpCallWithRetries.RetryDelaySeconds),
        //                                        httpCallWithRetries.RetryMaxRetries)
        //    {
        //        RetryTimeout = TimeSpan.FromSeconds(httpCallWithRetries.RetryTimeoutSeconds),
        //        MaxRetryInterval = TimeSpan.FromSeconds(httpCallWithRetries.RetryMaxDelaySeconds),
        //        BackoffCoefficient = httpCallWithRetries.RetryBackoffCoefficient
        //    };

        //    return ops;
        //}
        //public static RetryOptions GetRetryOptions(this IHttpCallWithRetries httpCallWithRetries)
        //{
        //    RetryOptions ops = new RetryOptions(TimeSpan.FromSeconds(httpCallWithRetries.RetryDelaySeconds),
        //                                        httpCallWithRetries.RetryMaxRetries)
        //    {
        //        RetryTimeout = TimeSpan.FromSeconds(httpCallWithRetries.RetryTimeoutSeconds),
        //        MaxRetryInterval = TimeSpan.FromSeconds(httpCallWithRetries.RetryMaxDelaySeconds),
        //        BackoffCoefficient = httpCallWithRetries.RetryBackoffCoefficient
        //    };

        //    return ops;
        //}

        
    }
}
