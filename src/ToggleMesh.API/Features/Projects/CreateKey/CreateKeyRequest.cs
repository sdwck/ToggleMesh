namespace ToggleMesh.API.Features.Projects.CreateKey;

public class CreateKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public KeyType Type { get; set; }
}