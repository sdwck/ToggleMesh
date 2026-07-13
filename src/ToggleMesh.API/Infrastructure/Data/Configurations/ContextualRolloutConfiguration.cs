using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public sealed class ContextualRolloutConfiguration : IEntityTypeConfiguration<ContextualRollout>
{
    public void Configure(EntityTypeBuilder<ContextualRollout> entity)
    {
        entity.HasKey(x => x.Id);
        entity.OwnsMany(x => x.Rollout, b => b.ToJson());
    }
}
