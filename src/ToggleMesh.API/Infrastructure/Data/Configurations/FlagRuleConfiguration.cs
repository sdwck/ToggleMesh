using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

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
            .HasMaxLength(256)
            .IsRequired();

        entity.OwnsMany(x => x.Rollout, b => b.ToJson());

        entity.HasOne(x => x.FlagEnvironmentState)
            .WithMany(x => x.Rules)
            .HasForeignKey(x => x.FlagEnvironmentStateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
