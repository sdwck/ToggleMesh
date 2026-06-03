using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Audit;

namespace ToggleMesh.API.Persistence.Configurations;

public sealed class AuditLogConfiguration
    : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.HasKey(x => x.Id);

        entity.Property(x => x.EntityName)
            .HasMaxLength(128)
            .IsRequired();
        
        entity.Property(x => x.EntityId)
            .HasMaxLength(128)
            .IsRequired();
        
        entity.Property(x => x.Action)
            .HasMaxLength(64)
            .IsRequired();
        
        entity.Property(x => x.PerformedBy)
            .HasMaxLength(256);
        
        entity.Property(x => x.OldValues)
            .HasColumnType("jsonb")
            .IsRequired(false);
        
        entity.Property(x => x.NewValues)
            .HasColumnType("jsonb")
            .IsRequired(false);
        
        entity.HasIndex(x => new { x.EnvironmentId, x.Timestamp });
        
        entity.HasIndex(x => new { x.EntityName, x.EntityId });
    }
}