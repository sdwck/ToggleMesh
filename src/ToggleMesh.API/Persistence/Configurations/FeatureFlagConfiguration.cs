using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Flags;

namespace ToggleMesh.API.Persistence.Configurations;

public sealed class FeatureFlagConfiguration
    : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> entity)
    {
        entity.ToTable("ProjectFeatureFlags");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Key)
            .HasMaxLength(256)
            .IsRequired();

        entity.HasIndex(x => new { x.ProjectId, x.Key })
            .IsUnique();

        entity.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}