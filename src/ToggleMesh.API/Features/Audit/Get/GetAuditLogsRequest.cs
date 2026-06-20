using ToggleMesh.Common.Pagination;

namespace ToggleMesh.API.Features.Audit.Get;

public class GetAuditLogsRequest : CursorPagedRequest
{
    public Guid? ProjectId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public string? SortOrder { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}