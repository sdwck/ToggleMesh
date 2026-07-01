using System.Text.Json.Serialization;
using ToggleMesh.Common;

namespace ToggleMesh.SDK.Models;

public record SdkGetFlagsResponse
{
    [JsonPropertyName("flags")]
    public List<FeatureFlagDto> Flags { get; init; } = [];

    [JsonPropertyName("segments")]
    public List<SegmentDto> Segments { get; init; } = [];
}
