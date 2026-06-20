namespace ToggleMesh.Common.Pagination;

public class CursorPagedRequest
{
    public Guid? Cursor { get; set; }
    public int PageSize { get; set; } = 20;
}