using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microflow.Helpers;
using System.Net;
using Microflow.Models;
using Microsoft.Azure.Cosmos.Table;

namespace Microflow.FlowControl
{
    /// <summary>
    /// "Microflow_InsertOrUpdateProject" must be called to save project step meta data to table storage
    /// after this, "Microflow_HttpStart" can be called multiple times,
    /// if a change is made to the project, call "Microflow_InsertOrUpdateProject" again to apply the changes
    /// </summary>
    public static class MicroflowStart
    {
        /// <summary>
        /// This is the entry point, project payload is in the http body
        /// </summary>
        /// <param name="instanceId">If an instanceId is passed in, it will run as a singleton, else it will run concurrently with each with a new instanceId</param>
        [FunctionName("Microflow_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "start/{projectName}/{instanceId?}")]
                                                                HttpRequestMessage req,
                                                                [DurableClient] IDurableOrchestrationClient client,
                                                                string instanceId, string projectName)
        {
            try
            {
                //await client.PurgeInstanceHistoryAsync("7c828621-3e7a-44aa-96fd-c6946763cc2b");

                ProjectRun projectRun = MicroflowStartupHelper.CreateStartupProjectRun(req.RequestUri.ParseQueryString(), ref instanceId, projectName);
                string baseUrl = $"{Environment.GetEnvironmentVariable("BaseUrl")}";
                projectRun.BaseUrl = baseUrl.EndsWith('/') ? baseUrl.Remove(baseUrl.Length - 1) : baseUrl;
                // start
                await client.StartNewAsync("Start", instanceId, projectRun);

                return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromSeconds(1));

            }
            catch (StorageException ex)
            {
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(ex.Message + " - Project in error state, call 'InsertOrUpdateProject' at least once before running a project.")
                };

                return resp;
            }
            catch (Exception e)
            {
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(e.Message)
                };

                return resp;
            }
        }

        /// <summary>
        /// This is called from Microflow_HttpStart, it does the looping and calls the ExecuteStep sub orchestration passing in the top step
        /// </summary>
        /// <returns></returns>
        [Deterministic]
        [FunctionName("Start")]
        public static async Task Start([OrchestrationTrigger] IDurableOrchestrationContext context,
                                       ILogger inLog)
        {
            ILogger log = context.CreateReplaySafeLogger(inLog);

            // read ProjectRun payload
            ProjectRun projectRun = context.GetInput<ProjectRun>();

            try
            {
                if (!await context.CheckAndWaitForReadyToRun(projectRun.ProjectName, log))
                {
                    return;
                }

                await context.StartMicroflowProject(log, projectRun);
            }
            catch (StorageException e)
            {
                // log to table workflow completed
                LogErrorEntity errorEntity = new LogErrorEntity(projectRun.ProjectName, Convert.ToInt32(projectRun.RunObject.StepNumber), e.Message, projectRun.RunObject.RunId);

                await context.CallActivityAsync("LogError", errorEntity);
            }
            catch (Exception e)
            {
                // log to table workflow completed
                LogErrorEntity errorEntity = new LogErrorEntity(projectRun.ProjectName, Convert.ToInt32(projectRun.RunObject.StepNumber), e.Message, projectRun.RunObject.RunId);

                await context.CallActivityAsync("LogError", errorEntity);
            }
        }


        /// <summary>
        /// Pause, run, or stop the project, cmd can be "run", "pause", or "stop"
        /// </summary>
        [FunctionName("Microflow_ProjectControl")]
        public static async Task<HttpResponseMessage> ProjectControl([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "ProjectControl/{cmd}/{projectName}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string projectName, string cmd)
        {
            EntityId runStateId = new EntityId(nameof(ProjectState), projectName);

            if (cmd.Equals(MicroflowControlKeys.Pause, StringComparison.OrdinalIgnoreCase))
            {
                await client.SignalEntityAsync(runStateId, MicroflowControlKeys.Pause);
            }
            else if (cmd.Equals(MicroflowControlKeys.Ready, StringComparison.OrdinalIgnoreCase))
            {
                await client.SignalEntityAsync(runStateId, MicroflowControlKeys.Pause);
            }
            else if (cmd.Equals(MicroflowControlKeys.Stop, StringComparison.OrdinalIgnoreCase))
            {
                await client.SignalEntityAsync(runStateId, MicroflowControlKeys.Stop);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Pause, run, or stop all with the same global key, cmd can be "run", "pause", or "stop"
        /// </summary>
        [FunctionName("Microflow_GlobalControl")]
        public static async Task<HttpResponseMessage> GlobalControl([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "GlobalControl/{cmd}/{globalKey}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string globalKey, string cmd)
        {
            EntityId runStateId = new EntityId(nameof(GlobalState), globalKey);

            if (cmd.Equals(MicroflowControlKeys.Pause, StringComparison.OrdinalIgnoreCase))
            {
                await client.SignalEntityAsync(runStateId, MicroflowControlKeys.Pause);
            }
            else if (cmd.Equals(MicroflowControlKeys.Ready, StringComparison.OrdinalIgnoreCase))
            {
                await client.SignalEntityAsync(runStateId, MicroflowControlKeys.Ready);
            }
            else if (cmd.Equals(MicroflowControlKeys.Stop, StringComparison.OrdinalIgnoreCase))
            {
                await client.SignalEntityAsync(runStateId, MicroflowControlKeys.Stop);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Durable entity check and set if the global state
        /// </summary>
        [FunctionName(MicroflowStateKeys.GlobalStateId)]
        public static void GlobalState([EntityTrigger] IDurableEntityContext ctx)
        {
            ctx.ResolveState();
        }

        /// <summary>
        /// Durable entity check and set project state
        /// </summary>
        [FunctionName(MicroflowStateKeys.ProjectStateId)]
        public static void ProjectState([EntityTrigger] IDurableEntityContext ctx)
        {
            ctx.ResolveState();
        }

        private static void ResolveState(this IDurableEntityContext ctx)
        {
            switch (ctx.OperationName)
            {
                case MicroflowControlKeys.Ready:
                    ctx.SetState(0);
                    break;
                case MicroflowControlKeys.Pause:
                    ctx.SetState(1);
                    break;
                case MicroflowControlKeys.Stop:
                    ctx.SetState(2);
                    break;
                case MicroflowControlKeys.Read:
                    ctx.Return(ctx.GetState<int>());
                    break;
            }
        }

        /// <summary>
        /// This must be called at least once before a project runs,
        /// this is to prevent multiple concurrent instances from writing step data at project run,
        /// call Microflow InsertOrUpdateProject when something changed in the workflow, but do not always call this when concurrent multiple workflows
        /// </summary>
        [FunctionName("Microflow_InsertOrUpdateProject")]
        public static async Task<HttpResponseMessage> SaveProject([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "InsertOrUpdateProject/{globalKey?}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string globalKey)
        {
            return await client.InserOrUpdateProject(await req.Content.ReadAsStringAsync(), globalKey);
        }

        /// <summary>
        /// Get global state
        /// </summary>
        [FunctionName("getGlobalState")]
        public static async Task<HttpResponseMessage> GetGlobalState([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "GlobalState/{globalKey}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string globalKey)
        {
            EntityId globalStateId = new EntityId(nameof(GlobalState), globalKey);
            Task<EntityStateResponse<int>> stateTask = client.ReadEntityStateAsync<int>(globalStateId);

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);

            await stateTask;

            resp.Content = new StringContent(stateTask.Result.EntityState.ToString());

            return resp;
        }

        /// <summary>
        /// Get project state
        /// </summary>
        [FunctionName("getProjectState")]
        public static async Task<HttpResponseMessage> GetProjectState([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "ProjectState/{projectName}")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client, string projectName)
        {
            EntityId runStateId = new EntityId(nameof(ProjectState), projectName);
            Task<EntityStateResponse<int>> stateTask = client.ReadEntityStateAsync<int>(runStateId);

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);

            await stateTask;

            resp.Content = new StringContent(stateTask.Result.EntityState.ToString());

            return resp;
        }
    }
}