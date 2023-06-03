using MicroflowModels;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace MicroflowSDK
{
    public static class ScaleGroupsManager
    {
        public static async Task<HttpResponseMessage> SetMaxInstanceCountForScaleGroup(string scaleGroupId, ScaleGroupState scaleGroupState, string baseUrl, HttpClient httpClient, int waitForResultSeconds = 30)
        {

            return await httpClient.PostAsJsonAsync($"{baseUrl}/ScaleGroup/{scaleGroupId}/{waitForResultSeconds}", scaleGroupState, new JsonSerializerOptions(JsonSerializerDefaults.General));
        }

        public static async Task<Dictionary<string, ScaleGroupState>> GetScaleGroupsWithMaxInstanceCounts(string scaleGroupId, string baseUrl, HttpClient httpClient)
        {
            Dictionary<string, ScaleGroupState> li = null;

            if (!string.IsNullOrWhiteSpace(scaleGroupId))
            {
                string t = await httpClient.GetStringAsync($"{baseUrl}/ScaleGroup/{scaleGroupId}");
                li = JsonSerializer.Deserialize<Dictionary<string, ScaleGroupState>>(t);
            }
            else
            {
                string t = await httpClient.GetStringAsync($"{baseUrl}/ScaleGroup");
                li = JsonSerializer.Deserialize<Dictionary<string, ScaleGroupState>>(t);
            }

            return li;
        }
    }
}
