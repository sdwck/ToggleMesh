namespace ToggleMesh.API.Features.Flags.GetPendingChanges;

public record PendingChangeDto(
    Guid Id,
    Guid FlagId,
    Guid EnvironmentId,
    Guid RequestedByUserId,
    string RequestedByUserName,
    string RequestedByUserEmail,
    Guid? ReviewedByUserId,
    string? ReviewedByUserName,
    string? ReviewedByUserEmail,
    List<Guid> ApprovedByUserIds,
    string Status,
    string PatchInstructionsJson,
    string? DiffSummaryJson,
    DateTimeOffset? ExecuteAt,
    bool IsPurelyScheduled,
    string? Comment,
    DateTimeOffset CreatedAt
);