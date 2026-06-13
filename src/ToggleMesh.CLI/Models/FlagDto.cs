using System.Text.Json.Serialization;

namespace ToggleMesh.CLI.Models;

public record FlagDto(string Key);

[JsonSerializable(typeof(List<FlagDto>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, 
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
internal partial class ToggleMeshJsonContext : JsonSerializerContext;