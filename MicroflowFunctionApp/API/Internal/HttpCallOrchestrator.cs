using System;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microflow.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microflow.API.Internal
{
    public static class MicroflowInternalApi
    {
        /// <summary>
        /// Inline http call, wait for response
        /// </summary>
        [Deterministic]
        [FunctionName("HttpCallOrchestrator")]
        public static async Task<MicroflowHttpResponse> HttpCallOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            HttpCall httpCall = context.GetInput<HttpCall>();

            bool doneAdd = false;
            bool doneSubtract = false;
            EntityId countId = new EntityId(MicroflowEntities.StepCounter, httpCall.PartitionKey + httpCall.RowKey);

            try
            {
                DurableHttpRequest durableHttpRequest = httpCall.CreateMicroflowDurableHttpRequest(context.InstanceId);

                // set the per step in-progress count to count+1
                context.SignalEntity(countId, MicroflowCounterKeys.Add);
                doneAdd = true;

                DurableHttpResponse durableHttpResponse = await context.CallHttpAsync(durableHttpRequest);

                // set the per step in-progress count to count-1
                context.SignalEntity(countId, MicroflowCounterKeys.Subtract);
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
                        HttpResponseStatusCode = -408,
                        Message = $"inline callout to {httpCall.CalloutUrl} timed out, StopOnActionFailed is false"
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
                        Message = $"inline callout to to {httpCall.CalloutUrl} failed, StopOnActionFailed is false - " + e.Message
                    };
                }

                throw;
            }
            finally
            {
                if (doneAdd && !doneSubtract)
                {
                    // set the per step in-progress count to count-1
                    context.SignalEntity(countId, MicroflowCounterKeys.Subtract);
                }
            }
        }
    }
}
