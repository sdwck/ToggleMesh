using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Segments.Domain;

namespace ToggleMesh.API.Features.Flags.SdkGetAll;

public record SdkGetFlagsResponse(
    IEnumerable<GetFlagResponse> Flags,
    IEnumerable<SegmentDto> Segments
);
