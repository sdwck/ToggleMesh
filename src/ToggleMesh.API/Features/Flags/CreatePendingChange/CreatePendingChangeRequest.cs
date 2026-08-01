namespace ToggleMesh.API.Features.Flags.CreatePendingChange;

public record CreatePendingChangeRequest(
    string PatchInstructionsJson,
    DateTimeOffset? ExecuteAt,
    string? Comment
);
