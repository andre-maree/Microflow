namespace Microflow.Models
{
    public interface IMicroflowHttpResponse
    {
        int HttpResponseStatusCode { get; set; }
        string Message { get; set; }
        bool Success { get; set; }
    }
}