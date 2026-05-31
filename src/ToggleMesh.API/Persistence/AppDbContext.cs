using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags;
using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectEnvironment> Environments { get; set; }
    public DbSet<EnvironmentKey> EnvironmentKeys { get; set; }
    public DbSet<FeatureFlag> FeatureFlags { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}