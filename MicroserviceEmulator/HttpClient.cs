using System.Net.Http;

namespace Microflow.API
{
    public class MicroflowHttpClient
    {
        // NB! To prevent port exaustion, use 1 static HttpClient for as much as possible
        // This instance of the HttpClient is also used in the ResponseProxyInlineDemoFunction
        // This client will be removed and is only included for the SleepTestOrchestrator
        public static readonly HttpClient HttpClient = new();
    }
}
