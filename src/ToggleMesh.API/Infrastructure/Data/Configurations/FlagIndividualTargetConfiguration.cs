using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public sealed class FlagIndividualTargetConfiguration : IEntityTypeConfiguration<FlagIndividualTarget>
{
    public void Configure(EntityTypeBuilder<FlagIndividualTarget> entity)
    {
        entity.HasKey(x => x.Id);
        
        entity.Property(x => x.IdentityKey)
            .HasMaxLength(256)
            .IsRequired();
            
        entity.HasIndex(x => new { x.FlagEnvironmentStateId, x.IdentityKey })
            .IsUnique();
    }
}
