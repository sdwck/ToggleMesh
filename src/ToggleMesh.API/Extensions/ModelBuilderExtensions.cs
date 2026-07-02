using Microsoft.EntityFrameworkCore;

namespace ToggleMesh.API.Extensions;

public static class ModelBuilderExtensions
{
    public static void ApplyGlobalUuidV7(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var pk = entityType.FindPrimaryKey();
            if (pk is { Properties.Count: 1 } && pk.Properties[0].ClrType == typeof(Guid))
                pk.Properties[0].SetValueGeneratorFactory((_, _) => new UuidV7ValueGenerator());
        }
    }
}