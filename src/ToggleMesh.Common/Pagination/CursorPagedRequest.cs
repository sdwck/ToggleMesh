namespace ToggleMesh.Common.Pagination;

public class CursorPagedRequest
{
    public string? Cursor { get; set; }
    public int PageSize { get; set; } = 20;
}