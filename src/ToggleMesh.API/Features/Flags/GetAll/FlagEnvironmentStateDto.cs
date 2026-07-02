namespace ToggleMesh.API.Features.Flags.GetAll;

public record FlagEnvironmentStateDto(
    Guid EnvironmentId,
    bool IsEnabled,
    int? RolloutPercentage,
    long TrueCount,
    long FalseCount,
    int RulesCount,
    bool IsMabEnabled,
    string? MabGoalEvent
);