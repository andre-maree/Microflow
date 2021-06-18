using System.Net.Http;

namespace Microflow.API
{
    public class MicroflowHttpClient
    {
        // NB! To prevent port exaustion, use 1 static HttpClient for as much as possible
        // This instance of the HttpClient is also used in the ResponseProxyInlineDemoFunction
        public static readonly HttpClient HttpClient = new HttpClient();
    }
}
