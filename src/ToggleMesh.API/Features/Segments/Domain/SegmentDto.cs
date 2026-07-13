using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Features.Segments.Domain;

public record SegmentDto(
    Guid Id,
    Guid EnvironmentId,
    string Name,
    string Description,
    IEnumerable<RuleInput> Rules,
    DateTimeOffset CreatedAt);

