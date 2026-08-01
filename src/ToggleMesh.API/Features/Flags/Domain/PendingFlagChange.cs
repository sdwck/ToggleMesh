using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data.Abstractions;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.Domain;

public class PendingFlagChange : AuditableEntity, ISoftDeletable
{
    public Guid FlagId { get; set; }
    public FeatureFlag Flag { get; set; } = null!;

    public Guid EnvironmentId { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;

    public Guid RequestedByUserId { get; set; }
    public ApplicationUser RequestedByUser { get; set; } = null!;

    public Guid? ReviewedByUserId { get; set; }
    public ApplicationUser? ReviewedByUser { get; set; }

    public List<Guid> ApprovedByUserIds { get; set; } = [];

    public PendingFlagChangeStatus Status { get; set; } = PendingFlagChangeStatus.PendingReview;

    public string PatchInstructionsJson { get; set; } = string.Empty;

    public string? DiffSummaryJson { get; set; }

    public DateTimeOffset? ExecuteAt { get; set; }

    public bool IsPurelyScheduled { get; set; }

    public string? Comment { get; set; }

    public bool IsDeleted { get; set; }
}
