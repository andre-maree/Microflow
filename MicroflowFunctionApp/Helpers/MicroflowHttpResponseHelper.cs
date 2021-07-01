using Microflow.Models;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Primitives;

namespace Microflow.Helpers
{
    public static class MicroflowHttpResponseHelper
    {
        /// <summary>
        /// Used to handle callout responses
        /// </summary>
        public static MicroflowHttpResponse GetMicroflowResponse(this DurableHttpResponse durableHttpResponse)
        {
            int statusCode = (int)durableHttpResponse.StatusCode;

            if (statusCode <= 200 || ((statusCode > 201) && (statusCode < 300)))
            {
                return new MicroflowHttpResponse() { Success = true, HttpResponseStatusCode = statusCode };
            }

            // if 201 created try get the location header to save it in the steps log
            if (statusCode != 201)
                return new MicroflowHttpResponse() {Success = false, HttpResponseStatusCode = statusCode};

            return durableHttpResponse.Headers.TryGetValue("location", out StringValues values)
                ? new MicroflowHttpResponse() {Success = true, HttpResponseStatusCode = statusCode, Message = values[0]}
                : new MicroflowHttpResponse() {Success = true, HttpResponseStatusCode = statusCode};
        }
    }
}
