namespace ToggleMesh.API.Features.Privacy.PurgeIdentity;

public class PurgeIdentityRequest
{
    public string Identity { get; set; } = null!;
    public Guid? EnvironmentId { get; set; }
}
