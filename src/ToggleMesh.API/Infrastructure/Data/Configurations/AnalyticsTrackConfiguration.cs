using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Analytics.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public class AnalyticsTrackConfiguration : IEntityTypeConfiguration<AnalyticsTrack>
{
    public void Configure(EntityTypeBuilder<AnalyticsTrack> builder)
    {
        builder.HasKey(x => new { x.Id, x.Timestamp });
        
        builder.Property(x => x.Properties).HasColumnType("jsonb");
        
        builder.HasIndex(x => x.EnvironmentId);
        builder.HasIndex(x => x.EventName);
        builder.HasIndex(x => x.Timestamp);
        
        builder.HasIndex(x => new { x.EnvironmentId, x.Identity, x.Timestamp });
    }
}
