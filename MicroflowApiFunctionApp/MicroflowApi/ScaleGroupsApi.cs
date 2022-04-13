#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_SCALEGROUPS && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_SCALEGROUPS && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
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
        /// Get/set max instance count for scale group
        /// </summary>
        [FunctionName("ScaleGroup")]
        public static async Task<HttpResponseMessage> ScaleGroup([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "ScaleGroup/{scaleGroupId?}/{maxInstanceCount?}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string scaleGroupId, int? maxInstanceCount)
        {
            if (req.Method.Equals(HttpMethod.Get))
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

            EntityId scaleGroupCountId = new(ScaleGroupCalls.ScaleGroupMaxConcurrentInstanceCount, scaleGroupId);

            await client.SignalEntityAsync(scaleGroupCountId, MicroflowEntityKeys.Set, maxInstanceCount);

            HttpResponseMessage resp = new(HttpStatusCode.OK);

            return resp;
        }
    }
}
#endif