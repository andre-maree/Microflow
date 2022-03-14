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
//using static Microflow.Helpers.Constants;

namespace MicroflowApiFunctionApp
{
    //public class MaxInstanceCount
    //{
    //    public string ScaleGroupId { get; set; }
    //    public int MaxCount { get; set; }
    //}

    public static class RunTimeApi
    {

        ///// <summary>
        ///// Pause, run, or stop the project, cmd can be "run", "pause", or "stop"
        ///// </summary>
        //[FunctionName("Microflow_ProjectControl")]
        //public static async Task<HttpResponseMessage> ProjectControl([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
        //                                                          Route = "ProjectControl/{cmd}/{projectName}")] HttpRequestMessage req,
        //                                                          [DurableClient] IDurableEntityClient client, string projectName, string cmd)
        //{
        //    return await client.SetRunState(nameof(ProjectState), projectName, cmd);
        //}

        ///// <summary>
        ///// Pause, run, or stop all with the same global key, cmd can be "run", "pause", or "stop"
        ///// </summary>
        //[FunctionName("Microflow_GlobalControl")]
        //public static async Task<HttpResponseMessage> GlobalControl([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
        //                                                          Route = "GlobalControl/{cmd}/{globalKey}")] HttpRequestMessage req,
        //                                                          [DurableClient] IDurableEntityClient client, string globalKey, string cmd)
        //{
        //    return await client.SetRunState(nameof(GlobalState), globalKey, cmd);
        //}

        ///// <summary>
        ///// Call this to see the step count in StepCallout per project name and stepId 
        ///// </summary>
        //[FunctionName("GetStepCountInprogress")]
        //public static async Task<int> GetStepCountInprogress([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "getstepcountinprogress/{projectNameStepNumber}")] HttpRequestMessage req,
        //                                                     [DurableClient] IDurableEntityClient client,
        //                                                     string projectNameStepNumber)
        //{
        //    EntityId countId = new EntityId(MicroflowEntities.StepCounter, projectNameStepNumber);

        //    EntityStateResponse<int> result = await client.ReadEntityStateAsync<int>(countId);

        //    return result.EntityState;
        //}

        /// <summary>
        /// Get global state
        /// </summary>
        //[FunctionName("getGlobalState")]
        //public static async Task<HttpResponseMessage> GetGlobalState([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
        //                                                          Route = "GlobalState/{globalKey}")] HttpRequestMessage req,
        //                                                          [DurableClient] IDurableEntityClient client, string globalKey)
        //{
        //    EntityId globalStateId = new EntityId("GlobalState", globalKey);
        //    Task<EntityStateResponse<int>> stateTask = client.ReadEntityStateAsync<int>(globalStateId);

        //    HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);

        //    await stateTask;

        //    resp.Content = new StringContent(stateTask.Result.EntityState.ToString());

        //    return resp;
        //}

        ///// <summary>
        ///// Get project state
        ///// </summary>
        //[FunctionName("getProjectState")]
        //public static async Task<HttpResponseMessage> GetProjectState([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
        //                                                          Route = "ProjectState/{projectName}")] HttpRequestMessage req,
        //                                                          [DurableClient] IDurableEntityClient client, string projectName)
        //{
        //    EntityId runStateId = new EntityId("ProjectState", projectName);
        //    Task<EntityStateResponse<int>> stateTask = client.ReadEntityStateAsync<int>(runStateId);

        //    HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);

        //    await stateTask;

        //    resp.Content = new StringContent(stateTask.Result.EntityState.ToString());

        //    return resp;
        //}

        /// <summary>
        /// Get project state
        /// </summary>
        [FunctionName("setScaleGroup")]
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