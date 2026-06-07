namespace ToggleMesh.API.Features.Flags.GetAll;

public record ProjectFlagDto(
    Guid Id,
    string Key,
    string? Name,
    string? Description,
    DateTime CreatedAt,
    IEnumerable<FlagEnvironmentStateDto> Environments
);