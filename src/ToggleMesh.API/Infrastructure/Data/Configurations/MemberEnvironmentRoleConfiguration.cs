using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public sealed class MemberEnvironmentRoleConfiguration : IEntityTypeConfiguration<MemberEnvironmentRole>
{
    public void Configure(EntityTypeBuilder<MemberEnvironmentRole> entity)
    {
        entity.HasKey(x => x.Id);

        entity.HasIndex(x => new { x.ProjectMemberId, x.EnvironmentId }).IsUnique();

        entity.HasOne(x => x.ProjectMember)
            .WithMany(x => x.EnvironmentRoles)
            .HasForeignKey(x => x.ProjectMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Environment)
            .WithMany()
            .HasForeignKey(x => x.EnvironmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
