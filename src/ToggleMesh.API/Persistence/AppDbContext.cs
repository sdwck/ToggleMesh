using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags;

namespace ToggleMesh.API.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<FeatureFlag> FeatureFlags { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FeatureFlag>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => x.Key).IsUnique();
        });
    }
}