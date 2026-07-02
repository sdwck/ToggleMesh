using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Features.Projects.GetKeys;

public class GetKeysResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public KeyType KeyType { get; set; }
    public string KeyPreview { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public DateTime? ExpireOn { get; set; }
    public DateTime? LastUsedAt { get; set; }
}