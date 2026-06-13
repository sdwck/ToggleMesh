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
        var url = !string.IsNullOrWhiteSpace(projectId) 
            ? $"api/v1/projects/{projectId}/flags" 
            : "api/v1/sdk/flags";
        
        var response = await _httpClient.GetAsync(url, ct);
        
        if (response.IsSuccessStatusCode) 
            return await response.Content.ReadFromJsonAsync<List<FlagDto>>(
                ToggleMeshJsonContext.Default.ListFlagDto, 
                ct) ?? [];
        
        var errorBody = await response.Content.ReadAsStringAsync(ct);
        throw new Exception($"API Request Failed [{(int)response.StatusCode}]. Details: {errorBody}");
    } 
}