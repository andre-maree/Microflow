using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using static Microflow.Helpers.Constants;

namespace MicroflowApiFunctionApp
{
    //public class MaxInstanceCount
    //{
    //    public string ScaleGroupId { get; set; }
    //    public int MaxCount { get; set; }
    //}

    public static class RunTimeApi
    {

        /// <summary>
        /// Pause, run, or stop the project, cmd can be "run", "pause", or "stop"
        /// </summary>
        [FunctionName("Microflow_ProjectControl")]
        public static async Task<HttpResponseMessage> ProjectControl([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "ProjectControl/{cmd}/{projectName}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string projectName, string cmd)
        {
            if (cmd.Equals(MicroflowControlKeys.Read, StringComparison.OrdinalIgnoreCase))
            {
                EntityId projStateId = new EntityId(MicroflowStateKeys.WorkflowState, projectName);
                EntityStateResponse<string> stateRes = await client.ReadEntityStateAsync<string>(projStateId);

                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(stateRes.EntityState)
                };

                return resp;
            }

            return await client.SetRunState(nameof(ProjectState), projectName, cmd);
        }

        /// <summary>
        /// Pause, run, or stop all with the same global key, cmd can be "run", "pause", or "stop"
        /// </summary>
        [FunctionName("Microflow_GlobalControl")]
        public static async Task<HttpResponseMessage> GlobalControl([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "GlobalControl/{cmd}/{globalKey}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string globalKey, string cmd)
        {
            if (cmd.Equals(MicroflowControlKeys.Read, StringComparison.OrdinalIgnoreCase))
            {
                EntityId globStateId = new EntityId(MicroflowStateKeys.GlobalState, globalKey);
                EntityStateResponse<string> stateRes = await client.ReadEntityStateAsync<string>(globStateId);

                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(stateRes.EntityState)
                };

                return resp;
            }

            return await client.SetRunState(nameof(GlobalState), globalKey, cmd);
        }

        /// <summary>
        /// Durable entity check and set if the global state
        /// </summary>
        [FunctionName(MicroflowStateKeys.GlobalState)]
        public static void GlobalState([EntityTrigger] IDurableEntityContext ctx)
        {
            ctx.RunState();
        }

        /// <summary>
        /// Durable entity check and set project state
        /// </summary>
        [FunctionName(MicroflowStateKeys.WorkflowState)]
        public static void ProjectState([EntityTrigger] IDurableEntityContext ctx)
        {
            ctx.RunState();
        }

        /// <summary>
        /// For project and global key states
        /// </summary>
        private static void RunState(this IDurableEntityContext ctx)
        {
            switch (ctx.OperationName)
            {
                case MicroflowControlKeys.Ready:
                    ctx.SetState(MicroflowStates.Ready);
                    break;
                case MicroflowControlKeys.Pause:
                    ctx.SetState(MicroflowStates.Paused);
                    break;
                case MicroflowControlKeys.Stop:
                    ctx.SetState(MicroflowStates.Stopped);
                    break;
                case MicroflowControlKeys.Read:
                    ctx.Return(ctx.GetState<int>());
                    break;
            }
        }
        /// <summary>
        /// Set the global or project state with the key, and the cmd can be "pause", "ready", or "stop"
        /// </summary>
        public static async Task<HttpResponseMessage> SetRunState(this IDurableEntityClient client,
                                                                   string stateEntityId,
                                                                   string key,
                                                                   string cmd)
        {
            EntityId runStateId = new EntityId(stateEntityId, key);

            switch (cmd)
            {
                case MicroflowControlKeys.Pause:
                    await client.SignalEntityAsync(runStateId, MicroflowControlKeys.Pause);
                    break;
                case MicroflowControlKeys.Ready:
                    await client.SignalEntityAsync(runStateId, MicroflowControlKeys.Ready);
                    break;
                case MicroflowControlKeys.Stop:
                    await client.SignalEntityAsync(runStateId, MicroflowControlKeys.Stop);
                    break;
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Get/set  project max instance count for scale group
        /// </summary>
        [FunctionName("ScaleGroup")]
        public static async Task<HttpResponseMessage> SetScaleGroup([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "ScaleGroup/{scaleGroupId?}/{maxInstanceCount?}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string scaleGroupId, int? maxInstanceCount)
        {
            if (req.Method.Equals(HttpMethod.Get))
            {
                Dictionary<string, int> result = new Dictionary<string, int>();
                EntityQueryResult res = null;

                using (CancellationTokenSource cts = new CancellationTokenSource())
                {
                    res = await client.ListEntitiesAsync(new EntityQuery()
                    {
                        PageSize = 99999999,
                        EntityName = "scalegroupmaxconcurrentinstancecount",
                        FetchState = true
                    }, cts.Token);
                }

                if (string.IsNullOrWhiteSpace(scaleGroupId))
                {
                    foreach (var rr in res.Entities)
                    {
                        result.Add(rr.EntityId.EntityKey, (int)rr.State);
                    }
                }
                else
                {
                    foreach (var rr in res.Entities.Where(e => e.EntityId.EntityKey.Equals(scaleGroupId)))
                    {
                        result.Add(rr.EntityId.EntityKey, (int)rr.State);
                    }
                }

                var content = new StringContent(JsonSerializer.Serialize(result));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content
                };
            }

            EntityId scaleGroupCountId = new EntityId("ScaleGroupMaxConcurrentInstanceCount", scaleGroupId);

            await client.SignalEntityAsync(scaleGroupCountId, "set", maxInstanceCount);

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);

            return resp;
        }

        [FunctionName("ScaleGroupMaxConcurrentInstanceCount")]
        public static void ScaleGroupMaxConcurrentInstanceCount([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName.ToLowerInvariant())
            {
                case "set":
                    ctx.SetState(ctx.GetInput<int>());
                    break;
                case "get":
                    ctx.Return(ctx.GetState<int>());
                    break;
                case "delete":
                    ctx.DeleteState();
                    break;
            }
        }
    }
}