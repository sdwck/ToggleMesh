namespace ToggleMesh.API.Features.Flags.GetPendingChanges;

public class GetPendingChangesRequest
{
    public Guid ProjectId { get; set; }
    public string Key { get; set; } = string.Empty;
    public Guid EnvironmentId { get; set; }
    public string? Cursor { get; set; }
    public int PageSize { get; set; } = 20;
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Status { get; set; }
    public bool ExcludePurelyScheduled { get; set; }
}