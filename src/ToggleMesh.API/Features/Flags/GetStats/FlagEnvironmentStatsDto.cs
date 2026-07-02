namespace ToggleMesh.API.Features.Flags.GetStats;

public record FlagEnvironmentStatsDto(Guid EnvironmentId, long TrueCount, long FalseCount);