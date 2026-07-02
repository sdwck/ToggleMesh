using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Analytics.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public class ExperimentMetricConfiguration : IEntityTypeConfiguration<ExperimentMetric>
{
    public void Configure(EntityTypeBuilder<ExperimentMetric> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.EnvironmentId, x.FlagKey, x.EventName, x.Variant })
            .IsUnique();
    }
}
