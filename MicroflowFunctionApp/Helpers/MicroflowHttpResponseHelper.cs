using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Collections.Generic;

namespace Microflow.Helpers
{
    public static class MicroflowHttpResponseHelper
    {
        public static MicroflowHttpResponse GetMicroflowResponse(this DurableHttpResponse durableHttpResponse)
        {
            int statusCode = (int)durableHttpResponse.StatusCode;

            if (statusCode <= 200 || ((statusCode > 201) && (statusCode < 300)))
            {
                return new MicroflowHttpResponse() { Success = true, HttpResponseStatusCode = statusCode};
            }
            else if (statusCode == 201)
            {
                Microsoft.Extensions.Primitives.StringValues values;

                if (durableHttpResponse.Headers.TryGetValue("location", out values))
                {
                    return new MicroflowHttpResponse() { Success = true, HttpResponseStatusCode = statusCode, Message = values[0] };
                }

                return new MicroflowHttpResponse() { Success = true, HttpResponseStatusCode = statusCode };
            }

            return new MicroflowHttpResponse() { Success = false, HttpResponseStatusCode = statusCode };
        }
    }
}
