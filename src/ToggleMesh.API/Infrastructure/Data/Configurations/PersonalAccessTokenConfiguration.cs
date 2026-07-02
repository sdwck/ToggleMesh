using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public sealed class PersonalAccessTokenConfiguration : IEntityTypeConfiguration<PersonalAccessToken>
{
    public void Configure(EntityTypeBuilder<PersonalAccessToken> entity)
    {
        entity.HasKey(x => x.Id);
        
        entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
        entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        entity.Property(x => x.TokenPreview).HasMaxLength(32).IsRequired();

        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => x.TokenHash).IsUnique();
        entity.HasIndex(x => x.UserId);
    }
}