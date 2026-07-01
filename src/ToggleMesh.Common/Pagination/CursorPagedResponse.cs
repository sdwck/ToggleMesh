namespace ToggleMesh.Common.Pagination;

public class CursorPagedResponse<T>
{
    public IEnumerable<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public string? NextCursor { get; set; }
    public bool HasNextPage { get; set; }
    
    
    public CursorPagedResponse() { }
    
    public CursorPagedResponse(IEnumerable<T> items, int totalCount, string? nextCursor = null, bool hasNextPage = false)
    {
        Items = items;
        TotalCount = totalCount;
        NextCursor = nextCursor;
        HasNextPage = hasNextPage;
    }
}