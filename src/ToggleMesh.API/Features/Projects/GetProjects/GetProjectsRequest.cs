using ToggleMesh.Common.Pagination;

namespace ToggleMesh.API.Features.Projects.GetProjects;

public class GetProjectsRequest : PagedRequest
{
    public Guid? OrganizationId { get; set; }
}