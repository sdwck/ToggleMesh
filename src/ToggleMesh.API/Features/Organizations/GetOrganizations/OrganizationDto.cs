namespace ToggleMesh.API.Features.Organizations.GetOrganizations;

public class OrganizationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public OrganizationRole Role { get; set; }
}