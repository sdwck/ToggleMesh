using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Audit;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Features.Flags;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Features.Webhooks;

namespace ToggleMesh.API.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
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

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}