using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Organizations.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> entity)
    {
        entity.HasKey(o => o.Id);
        
        entity.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(100);

        entity.HasMany(o => o.Projects)
            .WithOne(p => p.Organization)
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(o => o.Members)
            .WithOne(m => m.Organization)
            .HasForeignKey(m => m.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
        
        entity.HasQueryFilter(x => !x.IsDeleted);
    }
}
