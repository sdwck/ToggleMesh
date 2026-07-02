using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.HasQueryFilter("SoftDeletable", x => !x.IsDeleted);
    }
}
