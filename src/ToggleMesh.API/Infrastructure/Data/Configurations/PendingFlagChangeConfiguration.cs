using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public class PendingFlagChangeConfiguration : IEntityTypeConfiguration<PendingFlagChange>
{
    public void Configure(EntityTypeBuilder<PendingFlagChange> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Flag)
            .WithMany(x => x.PendingChanges)
            .HasForeignKey(x => x.FlagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Environment)
            .WithMany()
            .HasForeignKey(x => x.EnvironmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReviewedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PatchInstructionsJson)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();
    }
}
