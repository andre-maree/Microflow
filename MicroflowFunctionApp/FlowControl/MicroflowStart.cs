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
    }
}