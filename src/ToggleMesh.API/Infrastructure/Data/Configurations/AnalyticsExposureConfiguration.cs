using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Analytics.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public class AnalyticsExposureConfiguration : IEntityTypeConfiguration<AnalyticsExposure>
{
    public void Configure(EntityTypeBuilder<AnalyticsExposure> builder)
    {
        builder.HasKey(x => new { x.Id, x.Timestamp });
        
        builder.HasIndex(x => x.EnvironmentId);
        builder.HasIndex(x => x.FlagKey);
        builder.HasIndex(x => x.Timestamp);
        
        builder.HasIndex(x => new { x.EnvironmentId, x.Identity, x.Timestamp });
    }
}
