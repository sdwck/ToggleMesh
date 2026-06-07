using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Flags;

namespace ToggleMesh.API.Persistence.Configurations;

public sealed class FlagRuleConfiguration
    : IEntityTypeConfiguration<FlagRule>
{
    public void Configure(EntityTypeBuilder<FlagRule> entity)
    {
        entity.ToTable("ProjectFlagRules");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Attribute)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(x => x.Operator)
            .HasMaxLength(64)
            .IsRequired();

        entity.Property(x => x.Value)
            .HasMaxLength(1024)
            .IsRequired();

        entity.HasOne(x => x.FlagEnvironmentState)
            .WithMany(x => x.Rules)
            .HasForeignKey(x => x.FlagEnvironmentStateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}