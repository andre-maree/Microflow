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
        /// <param name="req"></param>
        /// <returns></returns>
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
                RunObject runObj = new RunObject() { StepId = -1 };
                projectRun.RunObject = runObj;

                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    instanceId = Guid.NewGuid().ToString();
                }

                projectRun.RunObject.StepId = -1;
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
                LogErrorEntity errorEntity = new LogErrorEntity(projectRun.ProjectName, e.Message, projectRun.RunObject.RunId);

                await context.CallActivityAsync("LogError", errorEntity);
            }
            catch (Exception e)
            {
                // log to table workflow completed
                LogErrorEntity errorEntity = new LogErrorEntity(projectRun.ProjectName, e.Message, projectRun.RunObject.RunId);

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
                                                                  Route = "InsertOrUpdateProject")] HttpRequestMessage req)
        {
            string content;

            MicroflowProject project = null;

            try
            {
                // read http content
                content = await req.Content.ReadAsStringAsync();

                // deserialize the workflow json
                project = JsonSerializer.Deserialize<MicroflowProject>(content);

                //    // create a project run
                ProjectRun projectRun = new ProjectRun() { ProjectName = project.ProjectName, Loop = project.Loop };

                // reate the storage tables for the project
                await MicroflowTableHelper.CreateTables(project.ProjectName);

                //  clear step table data
                Task delTask = projectRun.DeleteSteps();

                //    // parse the mergefields
                content.ParseMergeFields(ref project);

                await delTask;

                // prepare the workflow by persisting parent info to table storage
                await projectRun.PrepareWorkflow(project.Steps);

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
        }
    }
}
