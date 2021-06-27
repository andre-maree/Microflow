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
using Microsoft.Azure.Cosmos.Table;

namespace Microflow
{
    public static class MicroflowStart
    {
        /// <summary>
        /// This is the entry point, project payload is in the http body
        /// </summary>
        /// <param name="instanceId">If an instanceId is passed in, it will run as a singleton, else it will run concurrently with each with a new instanceId</param>
        /// <returns></returns>
        [FunctionName("Microflow_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "start/{instanceId?}")] HttpRequestMessage req,
                                                                [DurableClient] IDurableOrchestrationClient client,
                                                                string instanceId)
        {
            // read http content
            var content = await req.Content.ReadAsStringAsync();

            // deserialize the workflow json
            var projectbase = JsonSerializer.Deserialize<ProjectBase>(content);

            try
            {
                //await client.PurgeInstanceHistoryAsync("7c828621-3e7a-44aa-96fd-c6946763cc2b");

                // create a project run
                ProjectRun projectRun = new ProjectRun() { ProjectName = projectbase.ProjectName, Loop = projectbase.Loop };

                // set the state of the project to running
                await MicroflowTableHelper.UpdateStatetEntity(projectbase.ProjectName, 1);

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
            catch (StorageException e)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                resp.Content = new StringContent("Project in error state, call 'prepareproject/{instanceId?}' at least once before running a project.");
                
                return resp;
            }
            catch (Exception e)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                resp.Content = new StringContent(e.Message);

                return resp;
            }
        }

        /// <summary>
        /// This is called from Microflow_HttpStart, it does the looping and calls the ExecuteStep sub orchestration passing in the top step
        /// </summary>
        /// <returns></returns>
        [FunctionName("Start")]
        public static async Task Start([OrchestrationTrigger] IDurableOrchestrationContext context,
                                       ILogger inlog)
        {
            var log = context.CreateReplaySafeLogger(inlog);

            // read ProjectRun payload
            var projectRun = context.GetInput<ProjectRun>();

            try
            {
                // log start
                var logRowKey = MicroflowTableHelper.GetTableLogRowKeyDescendingByDate(context.CurrentUtcDateTime, "_" + projectRun.OrchestratorInstanceId);

                var logEntity = new LogOrchestrationEntity(true,
                                                           projectRun.ProjectName,
                                                           logRowKey,
                                                           $"{Environment.MachineName} - {projectRun.ProjectName} started...",
                                                           context.CurrentUtcDateTime,
                                                           projectRun.OrchestratorInstanceId);

                await context.CallActivityAsync("LogOrchestration", logEntity);

                log.LogInformation($"Started orchestration with ID = '{context.InstanceId}', Project = '{projectRun.ProjectName}'");

                await MicroflowHelper.StartProjectRun(context, log, projectRun);

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
                log.LogError("<<<<<<<<<<<<<<<<<<<<<<<<<-----> !!! A GREAT SUCCESS !!! <----->>>>>>>>>>>>>>>>>>>>>>>>>");
            }
            catch (StorageException e)
            {
                // log to table workflow completed
                var errorEntity = new LogErrorEntity(projectRun.ProjectName, e.Message, projectRun.RunObject.RunId);

                await context.CallActivityAsync("LogError", errorEntity);
            }
            catch (Exception e)
            {
                // log to table workflow completed
                var errorEntity = new LogErrorEntity(projectRun.ProjectName, e.Message, projectRun.RunObject.RunId);

                await context.CallActivityAsync("LogError", errorEntity);
            }
        }

        /// <summary>
        /// This must be called at least once before a project runs,
        /// this is to prevent multiple concurrent instances from writing step data at project run,
        /// call Microflow insertorupdateproject when something ischanges in the workflow, but do not always call this when corcurrent multiple workflows
        /// </summary>
        [FunctionName("Microflow_InsertOrUpdateProject")]
        public static async Task<HttpResponseMessage> SaveProject([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "InsertOrUpdateProject")] HttpRequestMessage req,
                                                                  [DurableClient] IDurableOrchestrationClient client)
        {
            // read http content
            var strWorkflow = await req.Content.ReadAsStringAsync();

            // deserialize the workflow json
            var project = JsonSerializer.Deserialize<Project>(strWorkflow);

            try
            {
                // create a project run
                ProjectRun projectRun = new ProjectRun() { ProjectName = project.ProjectName, Loop = project.Loop };

                // create the storage tables for the project
                await MicroflowTableHelper.CreateTables(project.ProjectName);

                // clear step table data
                await MicroflowTableHelper.DeleteSteps(projectRun);

                // parse the mergefields
                MicroflowHelper.ParseMergeFields(strWorkflow, ref project);

                // prepare the workflow by persisting parent info to table storage
                await MicroflowHelper.PrepareWorkflow(projectRun, project.Steps);

                return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, Guid.NewGuid().ToString(), TimeSpan.FromSeconds(1));

            }
            catch (StorageException e)
            {
                return await MicroflowHelper.LogError(project.ProjectName, e);
            }
            catch (Exception e)
            {
                return await MicroflowHelper.LogError(project.ProjectName, e);
            }
        }
    }
}
