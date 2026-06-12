namespace ToggleMesh.API.Features.Projects.EnvironmentKeys;

public class CreateKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public KeyType Type { get; set; }
}