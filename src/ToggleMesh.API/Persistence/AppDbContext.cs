using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Audit;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Features.Flags;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Features.Webhooks;
using ToggleMesh.API.Features.Organizations;

namespace ToggleMesh.API.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<OrganizationMember> OrganizationMembers { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectEnvironment> Environments { get; set; }
    public DbSet<EnvironmentKey> EnvironmentKeys { get; set; }
    public DbSet<FeatureFlag> FeatureFlags { get; set; }
    public DbSet<FlagEnvironmentState> FlagEnvironmentStates { get; set; }
    public DbSet<FlagRule> FlagRules { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<ProjectMember> ProjectMembers { get; set; }
    public DbSet<MemberEnvironmentRole> MemberEnvironmentRoles { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Webhook> Webhooks { get; set; }
    public DbSet<PersonalAccessToken> PersonalAccessTokens { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public async Task<(ProjectRole? Role, Dictionary<Guid, ProjectRole> EnvRoles)> GetProjectRoleAndEnvOverridesAsync(
        Guid projectId,
        Guid userId,
        CancellationToken ct = default)
    {
        var data = await Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new
            {
                p.OrganizationId,
                OrgMember = OrganizationMembers.FirstOrDefault(om =>
                    om.OrganizationId == p.OrganizationId &&
                    om.UserId == userId),
                ProjMember = ProjectMembers.Include(pm => pm.EnvironmentRoles)
                    .FirstOrDefault(pm => pm.ProjectId == p.Id && pm.UserId == userId)
            })
            .FirstOrDefaultAsync(ct);

        if (data == null) 
            return (null, []);

        if (data.OrgMember?.Role == OrganizationRole.Admin)
            return (ProjectRole.Owner, []);

        if (data.ProjMember == null)
            return (null, []);

        var envRoles =
            data.ProjMember
                .EnvironmentRoles
                .ToDictionary(er => 
                    er.EnvironmentId, 
                    er => er.Role);
        
        return (data.ProjMember.Role, envRoles);
    }
}