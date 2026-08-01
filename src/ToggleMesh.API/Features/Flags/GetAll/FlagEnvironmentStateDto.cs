namespace ToggleMesh.API.Features.Flags.GetAll;

public record FlagEnvironmentStateDto(
    Guid EnvironmentId,
    bool IsEnabled,
    bool HasRollout,
    long TrueCount,
    long FalseCount,
    int RulesCount,
    bool IsMabEnabled,
    string? MabGoalEvent,
    bool IsExperimentActive,
    bool HasChangesHistory = false,
    bool HasScheduledChanges = false,
    bool HasPendingApprovalsForCurrentUser = false
);
