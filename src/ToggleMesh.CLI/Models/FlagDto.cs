using System.Text.Json.Serialization;

namespace ToggleMesh.CLI.Models;

public record FlagDto(string Key);

[JsonSerializable(typeof(List<FlagDto>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
internal partial class ToggleMeshJsonContext : JsonSerializerContext;