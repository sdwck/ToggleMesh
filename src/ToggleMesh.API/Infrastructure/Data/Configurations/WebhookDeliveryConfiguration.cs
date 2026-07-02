using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Webhooks.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> entity)
    {
        entity.HasKey(x => x.Id);
        
        entity.Property(x => x.EventName)
            .HasMaxLength(128)
            .IsRequired();
        
        entity.Property(x => x.Payload)
            .HasColumnType("jsonb")
            .IsRequired();
            
        entity.Property(x => x.ErrorMessage)
            .HasMaxLength(2048);
            
        entity.HasOne(x => x.Webhook)
            .WithMany()
            .HasForeignKey(x => x.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);
            
        entity.HasIndex(x => x.WebhookId);
        
        entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
}
