using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Features.Projects.GetProjects;

public class ProjectListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProjectRole UserRole { get; set; }
    
    public int TotalFlags { get; set; }
    public int ActiveFlags { get; set; }
    public int RunningExperiments { get; set; }
    public int MabActiveFlagsCount { get; set; }
    public string? TopExperimentFlagKey { get; set; }
    public int FailingWebhooksCount { get; set; }
    public long Evaluations24H { get; set; }
}