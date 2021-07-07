using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Text.Json;
using Microflow.Helpers;
using MicroflowModels;
using System.Net;
using Microflow.Models;
using Microsoft.Azure.Cosmos.Table;
using System.Threading;

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
        public static async Task<HttpResponseMessage> HttpStart([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "start/{instanceId?}")]
                                                                HttpRequestMessage req,
                                                                [DurableClient] IDurableOrchestrationClient client,
                                                                string instanceId)
        {
            // read http content
            string content = await req.Content.ReadAsStringAsync();

            // deserialize the workflow json
            MicroflowProjectBase projectBase = JsonSerializer.Deserialize<MicroflowProjectBase>(content);

            try
            {
                //await client.PurgeInstanceHistoryAsync("7c828621-3e7a-44aa-96fd-c6946763cc2b");

                // create a project run
                ProjectRun projectRun = new ProjectRun() { ProjectName = projectBase.ProjectName, Loop = projectBase.Loop };

                // ERROR! cant do this
                //await MicroflowTableHelper.UpdateStatetEntity(projectBase.ProjectName, 1);

                // create a new run object
                RunObject runObj = new RunObject() { StepNumber = "-1" };
                projectRun.RunObject = runObj;

                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    instanceId = Guid.NewGuid().ToString();
                }

                projectRun.RunObject.StepNumber = "-1";
                projectRun.OrchestratorInstanceId = instanceId;

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
        [FunctionName("Start")]
        public static async Task Start([OrchestrationTrigger] IDurableOrchestrationContext context,
                                       ILogger inLog)
        {
            ILogger log = context.CreateReplaySafeLogger(inLog);

            // read ProjectRun payload
            ProjectRun projectRun = context.GetInput<ProjectRun>();

            try
            {
                await context.CheckAndWaitForReadyToRun(projectRun.ProjectName, log);

                // log start
                string logRowKey = MicroflowTableHelper.GetTableLogRowKeyDescendingByDate(context.CurrentUtcDateTime, "_" + projectRun.OrchestratorInstanceId);

                LogOrchestrationEntity logEntity = new LogOrchestrationEntity(true,
                                                           projectRun.ProjectName,
                                                           logRowKey,
                                                           $"{Environment.MachineName} - {projectRun.ProjectName} started...",
                                                           context.CurrentUtcDateTime,
                                                           projectRun.OrchestratorInstanceId);

                await context.CallActivityAsync("LogOrchestration", logEntity);

                log.LogInformation($"Started orchestration with ID = '{context.InstanceId}', Project = '{projectRun.ProjectName}'");

                await context.MicroflowStartProjectRun(log, projectRun);


                // log to table workflow completed
                logEntity = new LogOrchestrationEntity(false,
                                                       projectRun.ProjectName,
                                                       logRowKey,
                                                       $"{Environment.MachineName} - {projectRun.ProjectName} completed successfully",
                                                       context.CurrentUtcDateTime,
                                                       projectRun.OrchestratorInstanceId);

                await context.CallActivityAsync("LogOrchestration", logEntity);

                // done
                log.LogError($"Project run {projectRun.ProjectName} completed successfully...");
                log.LogError("<<<<<<<<<<<<<<<<<<<<<<<<<-----> !!! A GREAT SUCCESS  !!! <----->>>>>>>>>>>>>>>>>>>>>>>>>");
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
        /// This must be called at least once before a project runs,
        /// this is to prevent multiple concurrent instances from writing step data at project run,
        /// call Microflow InsertOrUpdateProject when something changed in the workflow, but do not always call this when concurrent multiple workflows
        /// </summary>
        [FunctionName("Microflow_InsertOrUpdateProject")]
        public static async Task<HttpResponseMessage> SaveProject([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
                                                                  Route = "InsertOrUpdateProject")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableEntityClient client)
        {
            bool doneReadyFalse = false;

            // read http content
            string content = await req.Content.ReadAsStringAsync();

            // deserialize the workflow json
            MicroflowProject project = JsonSerializer.Deserialize<MicroflowProject>(content);

            //    // create a project run
            ProjectRun projectRun = new ProjectRun() { ProjectName = project.ProjectName, Loop = project.Loop };

            EntityId readyToRun = new EntityId(nameof(ReadyToRun), projectRun.ProjectName);

            try
            {
                // set project ready to false
                await client.SignalEntityAsync(readyToRun, "false");
                doneReadyFalse = true;

                // reate the storage tables for the project
                await MicroflowTableHelper.CreateTables(project.ProjectName);

                //  clear step table data
                Task delTask = projectRun.DeleteSteps();

                //    // parse the mergefields
                content.ParseMergeFields(ref project);

                await delTask;

                // prepare the workflow by persisting parent info to table storage
                await projectRun.PrepareWorkflow(project.Steps, project.StepIdFormat);

                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }
            catch (StorageException e)
            {
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(e.Message)
                };

                return resp;
            }
            catch (Exception e)
            {
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(e.Message)
                };

                try
                {
                    await MicroflowHelper.LogError(project.ProjectName ?? "no project", e);
                }
                catch
                {
                    resp.StatusCode = HttpStatusCode.InternalServerError;
                }

                return resp;
            }
            finally
            {
                // if project ready was set to false, always set it to true
                if(doneReadyFalse)
                {
                    await client.SignalEntityAsync(readyToRun, "true");
                }
            }
        }
        
        /// <summary>
         /// Durable entity check if the project is ready to run
         /// </summary>
        [FunctionName("ReadyToRun")]
        public static void ReadyToRun([EntityTrigger] IDurableEntityContext ctx)
        {
            switch (ctx.OperationName.ToLowerInvariant())
            {
                case "true":
                    ctx.SetState(true);
                    break;
                case "false":
                    ctx.SetState(false);
                    break;
                case "get":
                    ctx.Return(ctx.GetState<bool>());
                    break;
            }
        }
    }
}
