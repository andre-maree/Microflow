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

namespace Microflow
{
    public static class MicroflowStart
    {

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

            // parse the mergefields
            MicroflowHelper.ParseMergeFields(strWorkflow, ref project);

            // await client.PurgeInstanceHistoryAsync("7c828621-3e7a-44aa-96fd-c6946763cc2b");

            // step 1 contains all the steps
            Step step1 = project.AllSteps;

            // create a project run
            ProjectRun projectRun = new ProjectRun() { ProjectId = project.ProjectName, Loop = project.Loop };

            // create the storage tables for the project
            await MicroflowTableHelper.CreateTables(project.ProjectName);

            // set the state of the project to running
            await MicroflowTableHelper.UpdateStatetEntity(project.ProjectName, 1);

            // create a new run object
            RunObject runObj = new RunObject() { StepId = step1.StepId };
            projectRun.RunObject = runObj;

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                instanceId = Guid.NewGuid().ToString();
            }
            // prepare the workflow by persisting parent info to table storage
            await MicroflowHelper.PrepareWorkflow(instanceId, projectRun, step1, project.MergeFields);

            // start
            await client.StartNewAsync("Start", instanceId, projectRun);

            return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId);
        }

        [FunctionName("Start")]
        public static async Task Start([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger inlog)
        {
            var log = context.CreateReplaySafeLogger(inlog);

            // read ProjectRun payload
            var projectRun = context.GetInput<ProjectRun>();

            log.LogInformation($"Started orchestration with ID = '{context.InstanceId}', Project = '{projectRun.ProjectId}'");

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
            RetryOptions retryOptions = new RetryOptions(TimeSpan.FromSeconds(15), 10)
            {
                MaxRetryInterval = TimeSpan.FromSeconds(1000),
                BackoffCoefficient = 1.5,
                RetryTimeout = TimeSpan.FromSeconds(30)
            };

            await context.CallSubOrchestratorWithRetryAsync("TableLogOrchestration", retryOptions, new LogOrchestrationEntity(projectRun.ProjectId, projectRun.RunObject.RunId, $"{Environment.MachineName} - {projectRun.ProjectId} completed successfully"));

            // done
            log.LogError("-------------------------------------------");
            log.LogError($"Project run {projectRun.ProjectId} completed successfully...");
            log.LogError("-------------------------------------------");
            log.LogError("<<<<<<<<<<<<<<<<<<<<<<<<<-----> !!! A GREAT SUCCESS !!! <----->>>>>>>>>>>>>>>>>>>>>>>>>");
        }
    }
}
