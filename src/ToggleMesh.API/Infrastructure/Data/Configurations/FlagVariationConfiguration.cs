using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public sealed class FlagVariationConfiguration : IEntityTypeConfiguration<FlagVariation>
{
    public void Configure(EntityTypeBuilder<FlagVariation> entity)
    {
        entity.HasKey(x => x.Id);
        
        entity.Property(x => x.Key)
            .HasMaxLength(256)
            .IsRequired();
            
        entity.HasIndex(x => new { x.FeatureFlagId, x.Key })
            .IsUnique();
            
        entity.HasOne(x => x.FeatureFlag)
            .WithMany(x => x.Variations)
            .HasForeignKey(x => x.FeatureFlagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
