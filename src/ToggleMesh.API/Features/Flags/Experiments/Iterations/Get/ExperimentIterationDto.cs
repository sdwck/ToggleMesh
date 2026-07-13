namespace ToggleMesh.API.Features.Flags.Experiments.Iterations.Get;

public record ExperimentIterationDto(
    Guid Id,
    Guid EnvironmentId,
    string FlagKey,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string FinalMetricsSnapshot,
    string FlagConfigSnapshot);
