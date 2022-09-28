#define INCLUDE_scalegroups
#if INCLUDE_scalegroups
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static MicroflowModels.Constants;

namespace MicroflowApi
{
    public class ScaleGroupsApi
    {
        /// <summary>
        /// Get max instance count for scale group
        /// </summary>
        [FunctionName("Get" + ScaleGroupCalls.ScaleGroup)]
        public static async Task<HttpResponseMessage> GetScaleGroup([HttpTrigger(AuthorizationLevel.Anonymous, "get",
                                                                  Route = MicroflowPath + "/ScaleGroup/{scaleGroupId}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string scaleGroupId)
        {
            Dictionary<string, int> result = new();
            EntityQueryResult res = null;

            using (CancellationTokenSource cts = new())
            {
                res = await client.ListEntitiesAsync(new EntityQuery()
                {
                    PageSize = 99999999,
                    EntityName = ScaleGroupCalls.ScaleGroupMaxConcurrentInstanceCount,
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

        /// <summary>
        /// Set the max instance count for scale group, default to wait for 5 seconds
        /// </summary>
        [FunctionName("Set" + ScaleGroupCalls.ScaleGroup)]
        public static async Task<HttpResponseMessage> ScaleGroup([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = MicroflowPath + "/ScaleGroup/{scaleGroupId}/{maxInstanceCount}/{maxWaitSeconds:int?}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableOrchestrationClient client, string scaleGroupId, int maxInstanceCount, int? maxWaitSeconds)
        {
            string instanceId = await client.StartNewAsync("SetScaleGroupMaxConcurrentCount", null, (scaleGroupId, maxInstanceCount));

            return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromSeconds(maxWaitSeconds is null
                                                                                   ? 5
                                                                                   : maxWaitSeconds.Value));
        }

        /// <summary>
        /// Set the scale group max concurrent instance count orchestration
        /// </summary>
        /// <returns></returns>
        [Deterministic]
        [FunctionName("SetScaleGroupMaxConcurrentCount")]
        public static async Task SetScaleGroupMaxOrchestration([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            (string scaleGroupId, int maxInstanceCount) input = context.GetInput<(string, int)>();

            EntityId scaleGroupCountId = new(ScaleGroupCalls.ScaleGroupMaxConcurrentInstanceCount, input.scaleGroupId);

            await context.CallEntityAsync(scaleGroupCountId, MicroflowEntityKeys.Set, input.maxInstanceCount);
        }
    }
}

#endif