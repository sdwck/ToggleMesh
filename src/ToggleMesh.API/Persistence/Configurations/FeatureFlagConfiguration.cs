using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Flags;

namespace ToggleMesh.API.Persistence.Configurations;

public sealed class FeatureFlagConfiguration
    : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> entity)
    {
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Key)
            .HasMaxLength(256)
            .IsRequired();

        entity.HasIndex(x => new { x.EnvironmentId, x.Key })
            .IsUnique();
    }
}