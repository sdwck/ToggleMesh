namespace ToggleMesh.API.Features.Flags.Domain;

public enum PendingFlagChangeStatus
{
    PendingReview = 0,
    Scheduled = 1,
    Executed = 2,
    Rejected = 3,
    ConflictFailed = 4,
    Cancelled = 5,
    Expired = 6
}
