using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Integrations.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public sealed class IntegrationConfiguration : IEntityTypeConfiguration<Integration>
{
    public void Configure(EntityTypeBuilder<Integration> entity)
    {
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Name)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.WebhookUrl)
            .HasMaxLength(2048)
            .IsRequired();

        entity.Property(x => x.Provider)
            .HasConversion<string>()
            .HasMaxLength(32);

        entity.Property(x => x.EnvironmentIds)
            .HasColumnType("uuid[]");

        entity.Property(x => x.Events)
            .HasColumnType("text[]");

        entity.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.ProjectId);
    }
}
