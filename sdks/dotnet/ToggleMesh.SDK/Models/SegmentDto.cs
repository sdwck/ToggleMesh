using System.Text.Json.Serialization;
using ToggleMesh.Common.Rules;

namespace ToggleMesh.SDK.Models;

public record SegmentDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("rules")]
    public IEnumerable<RuleDto> Rules { get; init; } = [];
}
