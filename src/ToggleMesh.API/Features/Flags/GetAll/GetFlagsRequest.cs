using ToggleMesh.Common.Pagination;
using ToggleMesh.API.Features.Flags;

namespace ToggleMesh.API.Features.Flags.GetAll;

public class GetFlagsRequest : PagedRequest
{
    public string Search { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
}