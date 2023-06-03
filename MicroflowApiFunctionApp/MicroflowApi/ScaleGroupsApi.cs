#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_SCALEGROUPS && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_SCALEGROUPS && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT
using MicroflowModels;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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
            Dictionary<string, ScaleGroupState> result = new();
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
                foreach (DurableEntityStatus rr in res.Entities)
                {
                    result.Add(rr.EntityId.EntityKey, rr.State.Value<ScaleGroupState>());
                }
            }
            else
            {
                foreach (DurableEntityStatus rr in res.Entities.Where(e => e.EntityId.EntityKey.Equals(scaleGroupId)))
                {
                    result.Add(rr.EntityId.EntityKey, rr.State.Value<ScaleGroupState>());
                }
            }

            StringContent content = new(JsonConvert.SerializeObject(result));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            };
        }

        /// <summary>
        /// Set the max instance count for scale group, default to wait for 5 seconds
        /// </summary>
        [FunctionName("Set" + ScaleGroupCalls.ScaleGroup)]
        public static async Task<HttpResponseMessage> ScaleGroup([HttpTrigger(AuthorizationLevel.Anonymous, "post",
                                                                  Route = MicroflowPath + "/ScaleGroup/{scaleGroupId}/{maxWaitSeconds:int?}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableOrchestrationClient client, string scaleGroupId, int? maxWaitSeconds)
        {
            ScaleGroupState state = JsonConvert.DeserializeObject<ScaleGroupState>(await req.Content.ReadAsStringAsync());

            string instanceId = await client.StartNewAsync("SetScaleGroupMaxConcurrentCount", null, (scaleGroupId, state));

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
            (string scaleGroupId, ScaleGroupState state) = context.GetInput<(string, ScaleGroupState)>();

            EntityId scaleGroupCountId = new(ScaleGroupCalls.ScaleGroupMaxConcurrentInstanceCount, scaleGroupId);

            await context.CallEntityAsync(scaleGroupCountId, MicroflowEntityKeys.Set, state);
        }
    }
}
#endif