namespace ToggleMesh.API.Features.Projects.EnvironmentKeys;

public class CreateKeyResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public KeyType KeyType { get; set; }
    public string KeyPreview { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public string PlainKey { get; set; } = string.Empty;
}