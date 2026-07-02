using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Features.Audit.Domain;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Metrics.Domain;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Segments.Domain;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Email.Models;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<OrganizationMember> OrganizationMembers { get; set; }
    public DbSet<OrganizationInvitation> OrganizationInvitations { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectEnvironment> Environments { get; set; }
    public DbSet<EnvironmentKey> EnvironmentKeys { get; set; }
    public DbSet<FeatureFlag> FeatureFlags { get; set; }
    public DbSet<FlagEnvironmentState> FlagEnvironmentStates { get; set; }
    public DbSet<ContextualRollout> ContextualRollouts { get; set; }
    public DbSet<FlagRule> FlagRules { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<ProjectMember> ProjectMembers { get; set; }
    public DbSet<MemberEnvironmentRole> MemberEnvironmentRoles { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Webhook> Webhooks { get; set; }
    public DbSet<WebhookDelivery> WebhookDeliveries { get; set; }
    public DbSet<PersonalAccessToken> PersonalAccessTokens { get; set; }
    public DbSet<EmailOutboxMessage> EmailOutboxMessages { get; set; }
    public DbSet<AnalyticsExposure> AnalyticsExposures { get; set; }
    public DbSet<AnalyticsTrack> AnalyticsTracks { get; set; }
    public DbSet<ExperimentMetric> ExperimentMetrics { get; set; }
    public DbSet<ContextualExperimentMetric> ContextualExperimentMetrics { get; set; }
    public DbSet<ExperimentIteration> ExperimentIterations { get; set; }
    public DbSet<Segment> Segments { get; set; }
    public DbSet<SegmentRule> SegmentRules { get; set; }
    public DbSet<FlagMetricBucket> FlagMetricBuckets { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ApplyGlobalUuidV7();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(warnings => 
            warnings.Ignore(CoreEventId
                .PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }

}