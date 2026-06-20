namespace ToggleMesh.API.Features.Flags.GetAll;

public record ProjectFlagDto(
    Guid Id,
    string Key,
    string? Name,
    string? Description,
    bool IsClientSideExposed,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IEnumerable<FlagEnvironmentStateDto> Environments,
    string[] Tags
);