namespace MicroflowModels
{
    public interface IHttpCallWithRetries : IHttpCall
    {
        double RetryBackoffCoefficient { get; set; }
        int RetryDelaySeconds { get; set; }
        int RetryMaxDelaySeconds { get; set; }
        int RetryMaxRetries { get; set; }
        int RetryTimeoutSeconds { get; set; }
    }
}