using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Segments.Domain;

public class SegmentRule : AuditableEntity
{
    public Guid SegmentId { get; set; }
    public Segment Segment { get; set; } = null!;
    
    public int GroupId { get; set; } 
    public string Attribute { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
