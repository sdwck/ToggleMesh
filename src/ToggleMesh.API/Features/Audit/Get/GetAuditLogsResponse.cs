namespace ToggleMesh.API.Features.Audit.Get;

public class GetAuditLogsResponse
{
    public List<AuditLogDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}