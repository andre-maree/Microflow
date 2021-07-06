using System;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.API.Internal
{
    public static class MicroflowInternalAPI
    {
        /// <summary>
        /// Inline http call, wait for response
        /// </summary>
        [FunctionName("HttpCallOrchestrator")]
        public static async Task<MicroflowHttpResponse> HttpCallOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            HttpCall httpCall = context.GetInput<HttpCall>();

            bool doneAdd = false;
            bool doneSubtract = false;
            EntityId countId = new EntityId("StepCounter", httpCall.PartitionKey + httpCall.RowKey);

            try
            {
                DurableHttpRequest durableHttpRequest = httpCall.CreateMicroflowDurableHttpRequest(context.InstanceId);

                // set the per step in-progress count to count+1
                context.SignalEntity(countId, "add");
                doneAdd = true;

                DurableHttpResponse durableHttpResponse = await context.CallHttpAsync(durableHttpRequest);

                // set the per step in-progress count to count-1
                context.SignalEntity(countId, "subtract");
                doneSubtract = true;

                return durableHttpResponse.GetMicroflowResponse();
            }
            catch (TimeoutException)
            {
                if (!httpCall.StopOnActionFailed)
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = false,
                        HttpResponseStatusCode = 408,
                        Message = $"callback action {httpCall.CallBackAction} timed out"
                    };
                }

                throw;
            }
            catch (Exception e)
            {
                if (!httpCall.StopOnActionFailed)
                {
                    return new MicroflowHttpResponse()
                    {
                        Success = false,
                        HttpResponseStatusCode = -999,
                        Message = $"callback action {httpCall.CallBackAction} failed - " + e.Message
                    };
                }

                throw;
            }
            finally
            {
                if (doneAdd && !doneSubtract)
                {
                    // set the per step in-progress count to count-1
                    context.SignalEntity(countId, "subtract");
                }
            }
        }
    }
}
