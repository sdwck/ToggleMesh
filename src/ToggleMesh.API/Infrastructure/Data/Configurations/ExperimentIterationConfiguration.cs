using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Analytics.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public class ExperimentIterationConfiguration : IEntityTypeConfiguration<ExperimentIteration>
{
    public void Configure(EntityTypeBuilder<ExperimentIteration> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.FlagKey)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.FinalMetricsSnapshot)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(x => new { x.EnvironmentId, x.FlagKey });
    }
}
