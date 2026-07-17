namespace ToggleMesh.API.Features.Organizations.UpdateOrganization;

public class UpdateOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public bool RequireTwoFactor { get; set; }
}
