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

    public async Task<List<FlagDto>> GetAllFlagsAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("api/v1/sdk/flags", ct);
        
        if (response.IsSuccessStatusCode) 
            return await response.Content.ReadFromJsonAsync<List<FlagDto>>(
                ToggleMeshJsonContext.Default.ListFlagDto, 
                ct) ?? [];
        
        var errorBody = await response.Content.ReadAsStringAsync(ct);
        throw new Exception($"API Request Failed [{(int)response.StatusCode}]. Details: {errorBody}");
    } 
}