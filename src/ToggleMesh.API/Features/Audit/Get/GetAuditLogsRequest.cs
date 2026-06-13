namespace ToggleMesh.API.Features.Audit.Get;

public class GetAuditLogsRequest
{
    public Guid? ProjectId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public string? SortOrder { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}