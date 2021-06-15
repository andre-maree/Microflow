using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Microflow.API
{
    public static class MicroflowAPI
    {
        // NB! To prevent port exaustion, use 1 static HttpClient for as much as possible
        // This instance of the HttpClient is also used in the ResponseProxyCallbackDemoFunction
        public static readonly HttpClient MicroflowHttpClient = new HttpClient();

        /// <summary>
        /// The callout is already made in
        /// </summary>
        /// <returns>True or false to indicate success</returns>
        [FunctionName("HttpCallWithCallbackOrchestrator")]
        public static async Task<bool> HttpCallWithCallbackOrchestrator(
   [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger inlog)
        {
            var log = context.CreateReplaySafeLogger(inlog);

            var httpCall = context.GetInput<HttpCall>();

            DurableHttpRequest durableHttpRequest = MicroflowHelper.GetDurableHttpRequest(httpCall, context.InstanceId);

            // http call outside of Microflow, this is the micro-service api call
            var result = await context.CallHttpAsync(durableHttpRequest);

            if (result.StatusCode != System.Net.HttpStatusCode.OK)
                return false;

            log.LogCritical($"Waiting for callback: http://localhost:7071/api/{httpCall.CallBackAction}/{context.InstanceId}/{httpCall.RowKey}");

            // wait for the external event, set the timeout
            var actionResult = await context.WaitForExternalEvent<string>(httpCall.CallBackAction, TimeSpan.FromSeconds(httpCall.ActionTimeoutSeconds));

            // check for action failed, failed can be when result is fail, input from externally, or the timeout
            if (!string.IsNullOrEmpty(actionResult) && actionResult.Equals("fail"))
            {
                log.LogWarning($"Step {httpCall.RowKey} callback action {httpCall.CallBackAction} denied at {DateTime.Now.ToString("HH:mm:ss")}");

                // return false immdiately to stop the workflow processing
                if (httpCall.StopOnActionFailed)
                    return false;

                return true;
            }
            else
            {
                log.LogWarning($"Step {httpCall.RowKey} callback action {httpCall.CallBackAction} successful at {DateTime.Now.ToString("HH:mm:ss")}");

                return true;
            }
        }

        /// <summary>
        /// Inline http call, wait for response
        /// </summary>
        /// <returns>True or false to indicate success</returns>
        [FunctionName("HttpCallOrchestrator")]
        public static async Task<bool> HttpCallOrchestrator(
   [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var httpCall = context.GetInput<HttpCall>();

            DurableHttpRequest durableHttpRequest = MicroflowHelper.GetDurableHttpRequest(httpCall, context.InstanceId);

            // http call outside of Microflow, this is the micro-service api call
            var result = await context.CallHttpAsync(durableHttpRequest);

            if (result.StatusCode == System.Net.HttpStatusCode.OK)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Called from FunctionFlow.ExecuteStep to get the current step table config
        /// </summary>
        [FunctionName("GetStep")]
        public static async Task<HttpCallWithRetries> GetStep([ActivityTrigger] ProjectRun projectRun)
        {
            try
            {
                return await MicroflowHelper.GetStep(projectRun);
            }
            catch (StorageException)
            {
                throw;
            }
        }

        /// <summary>
        /// Called from FunctionFlow.ExecuteStep to get the current state of the project
        /// </summary>
        [FunctionName("GetState")]
        public static async Task<int> GetState([ActivityTrigger] string projectId)
        {
            try
            {
                return await MicroflowHelper.GetState(projectId);
            }
            catch (StorageException ex)
            {
                throw;
            }
        }

        /// <summary>
        /// Called from FunctionFlow.ExecuteStep to get the current state of the project
        /// </summary>
        [FunctionName("GetProjectControl")]
        public static async Task<ProjectControlEntity> GetProjectControl([ActivityTrigger] string projectId)
        {
            try
            {
                return await MicroflowHelper.GetProjectControl(projectId);
            }
            catch (StorageException ex)
            {
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        [FunctionName("Pause")]
        public static async Task Pause([ActivityTrigger] ProjectControlEntity projectControlEntity)
        {
            try
            {
                await MicroflowHelper.Pause(projectControlEntity);
            }
            catch (StorageException ex)
            {
                throw;
            }
        }

        /// <summary>
        /// use this to test some things like causing an exception
        /// </summary>
        [FunctionName("testpost")]
        public static async Task<HttpResponseMessage> TestPost(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "testpost")] HttpRequestMessage req)
        {
            await Task.Delay(15000);
            await req.Content.ReadAsStringAsync();

            //MicroflowPostData result = JsonSerializer.Deserialize<MicroflowPostData>(r);

            //if (result.StepId.Equals("1"))
            //{
            //    return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            //}

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }
}
