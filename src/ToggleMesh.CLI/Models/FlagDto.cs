using System.Text.Json.Serialization;

namespace ToggleMesh.CLI.Models;

public record FlagDto(string Key);
public record SdkFlagsResponse(List<FlagDto> Flags);
public record ProjectFlagsResponse(List<FlagDto> Items);

[JsonSerializable(typeof(SdkFlagsResponse))]
[JsonSerializable(typeof(ProjectFlagsResponse))]
[JsonSerializable(typeof(List<FlagDto>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, 
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
internal partial class ToggleMeshJsonContext : JsonSerializerContext;