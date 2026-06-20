using ToggleMesh.Common.Pagination;

namespace ToggleMesh.API.Features.Flags.GetAll;

public class GetFlagsRequest : CursorPagedRequest
{
    public string Search { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
}