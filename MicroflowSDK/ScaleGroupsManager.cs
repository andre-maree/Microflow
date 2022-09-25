using MicroflowModels;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace MicroflowSDK
{
    public static class ScaleGroupsManager
    {
        public static async Task<HttpResponseMessage> SetMaxInstanceCountForScaleGroup(string scaleGroupId, int maxInstanceCount, string baseUrl, HttpClient httpClient)
        {
            return await httpClient.PostAsJsonAsync($"{baseUrl}/ScaleGroup/{scaleGroupId}/{maxInstanceCount}", new JsonSerializerOptions(JsonSerializerDefaults.General));
        }

        public static async Task<Dictionary<string, int>> GetScaleGroupsWithMaxInstanceCounts(string scaleGroupId, string baseUrl, HttpClient httpClient)
        {
            Dictionary<string, int> li = null;

            if (!string.IsNullOrWhiteSpace(scaleGroupId))
            {
                string t = await httpClient.GetStringAsync($"{baseUrl}/ScaleGroup/{scaleGroupId}");
                li = JsonSerializer.Deserialize<Dictionary<string, int>>(t);
            }
            else
            {
                string t = await httpClient.GetStringAsync($"{baseUrl}/ScaleGroup");
                li = JsonSerializer.Deserialize<Dictionary<string, int>>(t);
            }

            return li;
        }
    }
}
