using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Metrics.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public sealed class FlagMetricBucketConfiguration : IEntityTypeConfiguration<FlagMetricBucket>
{
    public void Configure(EntityTypeBuilder<FlagMetricBucket> entity)
    {
        entity.HasKey(x => new { x.EnvironmentId, x.FlagKey, x.TimestampBucket });

        entity.HasOne(x => x.Environment)
            .WithMany()
            .HasForeignKey(x => x.EnvironmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
