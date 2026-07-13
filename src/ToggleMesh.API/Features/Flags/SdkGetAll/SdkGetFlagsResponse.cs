using ToggleMesh.API.Features.Segments.Domain;
using ToggleMesh.Common;

namespace ToggleMesh.API.Features.Flags.SdkGetAll;

public record SdkGetFlagsResponse(
    IEnumerable<FeatureFlagDto> Flags,
    IEnumerable<SegmentDto> Segments
);
