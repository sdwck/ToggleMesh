using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

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
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        entity.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasQueryFilter(x => !x.IsDeleted);
    }
}