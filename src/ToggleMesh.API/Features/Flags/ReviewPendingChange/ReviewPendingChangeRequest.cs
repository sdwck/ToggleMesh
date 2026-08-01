namespace ToggleMesh.API.Features.Flags.ReviewPendingChange;

public record ReviewPendingChangeRequest(
    ReviewAction Action,
    string? Comment = null
);
