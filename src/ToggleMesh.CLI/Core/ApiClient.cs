using System.Net.Http.Json;
using ToggleMesh.CLI.Models;

namespace ToggleMesh.CLI.Core;

public class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<FlagDto>> GetAllFlagsAsync(string? projectId = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            var url = $"api/v1/projects/{projectId}/flags";
            var response = await _httpClient.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var paged = await response.Content.ReadFromJsonAsync<ProjectFlagsResponse>(
                    ToggleMeshJsonContext.Default.ProjectFlagsResponse, ct);
                return paged?.Data ?? [];
            }
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"API Request Failed [{(int)response.StatusCode}]. Details: {errorBody}");
        }
        else
        {
            var url = "api/v1/sdk/flags";
            var response = await _httpClient.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var sdkResp = await response.Content.ReadFromJsonAsync<SdkFlagsResponse>(
                    ToggleMeshJsonContext.Default.SdkFlagsResponse, ct);
                return sdkResp?.Flags ?? [];
            }
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"API Request Failed [{(int)response.StatusCode}]. Details: {errorBody}");
        }
    } 
}