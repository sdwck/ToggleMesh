using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public sealed class ProjectEnvironmentConfiguration
    : IEntityTypeConfiguration<ProjectEnvironment>
{
    public void Configure(EntityTypeBuilder<ProjectEnvironment> entity)
    {
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        entity.Property(x => x.Name)
            .HasMaxLength(256)
            .IsRequired();

        entity.HasMany(x => x.Keys)
            .WithOne(x => x.Environment)
            .HasForeignKey(x => x.EnvironmentId)
            .OnDelete(DeleteBehavior.Cascade);
        
        entity.HasQueryFilter(x => !x.IsDeleted);
    }
}
