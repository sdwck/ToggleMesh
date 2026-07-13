namespace ToggleMesh.API.Features.Flags.GetStats;

public record FlagEnvironmentStatsDto(Guid EnvironmentId, Dictionary<Guid, long> VariationsCount);
