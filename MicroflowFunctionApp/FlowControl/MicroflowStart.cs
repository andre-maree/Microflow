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
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
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
        public static async Task<HttpResponseMessage> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "start/{instanceId?}")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient client,
            string instanceId)
        {
            // read http content
            var strWorkflow = await req.Content.ReadAsStringAsync();

            // deserialize the workflow json
            var project = JsonSerializer.Deserialize<Project>(strWorkflow);

            try
            {

                //await client.PurgeInstanceHistoryAsync("7c828621-3e7a-44aa-96fd-c6946763cc2b");

                // step 1 contains all the steps
                List<Step> steps = project.Steps;

                // create a project run
                ProjectRun projectRun = new ProjectRun() { ProjectName = project.ProjectName, Loop = project.Loop };

                // set the state of the project to running
                await MicroflowTableHelper.UpdateStatetEntity(project.ProjectName, 1);

                // create a new run object
                RunObject runObj = new RunObject() { StepId = steps[0].StepId };
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
        public static async Task Start([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger inlog)
        {
            var log = context.CreateReplaySafeLogger(inlog);

            // read ProjectRun payload
            var projectRun = context.GetInput<ProjectRun>();

            try
            {
                // log start
                var logRowKey = context.InstanceId.GetTableRowKeyDescendingByDate(context.CurrentUtcDateTime);
                var logEntity = new LogOrchestrationEntity(true, projectRun.ProjectName, logRowKey,
                    $"{Environment.MachineName} - {projectRun.ProjectName} started...", context.CurrentUtcDateTime);
                await context.CallActivityAsync("LogOrchestration", logEntity);

                log.LogInformation(
                    $"Started orchestration with ID = '{context.InstanceId}', Project = '{projectRun.ProjectName}'");

                // do the looping
                for (int i = 1; i <= projectRun.Loop; i++)
                {
                    var guid = context.NewGuid().ToString();

                    projectRun.RunObject.RunId = guid;

                    log.LogError($"Started Run ID {projectRun.RunObject.RunId}...");

                    // pass in the current loop count so it can be used downstream/passed to the micro-services
                    projectRun.CurrentLoop = i;

                    await context.CallSubOrchestratorAsync("ExecuteStep", guid, projectRun);

                    log.LogError($"Run ID {guid} completed successfully...");
                }

                // log to table workflow completed
                logEntity = new LogOrchestrationEntity(false, projectRun.ProjectName, logRowKey,
                    $"{Environment.MachineName} - {projectRun.ProjectName} completed successfully",
                    context.CurrentUtcDateTime);
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
        /// this is to prevent multiple concurrent instances from writing step data at project run
        /// </summary>
        [FunctionName("Microflow_PrepareProject")]
        public static async Task<HttpResponseMessage> SaveProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "prepareproject/{instanceId?}")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient client,
            string instanceId)
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
                await MicroflowHelper.PrepareWorkflow(instanceId, projectRun, project.Steps, project.MergeFields);

                return new HttpResponseMessage(HttpStatusCode.OK); //await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromSeconds(1));

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
