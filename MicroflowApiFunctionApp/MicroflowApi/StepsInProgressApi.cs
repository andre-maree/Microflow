#if DEBUG || RELEASE || !DEBUG_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !DEBUG_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !DEBUG_NO_UPSERT_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_FLOWCONTROL_STEPCOUNT && !RELEASE_NO_UPSERT_SCALEGROUPS_STEPCOUNT && !RELEASE_NO_UPSERT_STEPCOUNT
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace MicroflowApi
{
    public class StepsInProgressApi
    {
        //[OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        //[OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        //[OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        //[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        [FunctionName("GetStepsCountInProgress")]
        public static async Task<int> GetStepsCountInProgress([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = MicroflowModels.Constants.MicroflowVersion + "/GetStepsCountInProgress/{workflowNameStepNumber}")] HttpRequestMessage req,
                                                             [DurableClient] IDurableEntityClient client,
                                                             string workflowNameStepNumber)
        {
            EntityId countId = new EntityId("StepCount", workflowNameStepNumber);

            EntityStateResponse<int> result = await client.ReadEntityStateAsync<int>(countId);

            return result.EntityState;
        }
    }
}
#endif