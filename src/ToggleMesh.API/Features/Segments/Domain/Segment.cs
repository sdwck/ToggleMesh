using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Segments.Domain;

public class Segment : AuditableEntity
{
    public Guid EnvironmentId { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;
    
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<SegmentRule> Rules { get; set; } = new List<SegmentRule>();
}
