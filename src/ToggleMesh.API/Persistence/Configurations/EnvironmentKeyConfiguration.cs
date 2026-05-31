using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Persistence.Configurations;

public sealed class EnvironmentKeyConfiguration
    : IEntityTypeConfiguration<EnvironmentKey>
{
    public void Configure(EntityTypeBuilder<EnvironmentKey> entity)
    {
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        entity.Property(x => x.ApiKey)
            .HasMaxLength(64)
            .IsRequired();

        entity.HasIndex(x => x.ApiKey)
            .IsUnique();
    }
}