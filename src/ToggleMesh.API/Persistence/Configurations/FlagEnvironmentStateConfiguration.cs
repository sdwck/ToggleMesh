using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Flags;

namespace ToggleMesh.API.Persistence.Configurations;

public sealed class FlagEnvironmentStateConfiguration : IEntityTypeConfiguration<FlagEnvironmentState>
{
    public void Configure(EntityTypeBuilder<FlagEnvironmentState> entity)
    {
        entity.HasKey(x => x.Id);

        entity.HasIndex(x => new { x.FeatureFlagId, x.EnvironmentId })
            .IsUnique();

        entity.HasOne(x => x.FeatureFlag)
            .WithMany(x => x.States)
            .HasForeignKey(x => x.FeatureFlagId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Environment)
            .WithMany()
            .HasForeignKey(x => x.EnvironmentId)
            .OnDelete(DeleteBehavior.Cascade);
            
        entity.HasMany(x => x.Rules)
            .WithOne(x => x.FlagEnvironmentState)
            .HasForeignKey(x => x.FlagEnvironmentStateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
