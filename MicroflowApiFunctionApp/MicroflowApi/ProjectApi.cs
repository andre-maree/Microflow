using System.Net.Http;
using System.Threading.Tasks;
using Microflow.Helpers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace MicroflowApiFunctionApp
{
    public static class ProjectApi
    {
        /// <summary>
        /// Call this to get the Json data of the project
        /// </summary>
        [FunctionName("GetProjectJson")]
        public static async Task<string> GetProjectJsonWithMergefieldsReplaced([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "GetProjectJson/{projectName}")] HttpRequestMessage req,
                                                        string projectName)
        {
            return await MicroflowTableHelper.GetProjectAsJson(projectName);
        }
    }
}