using Microsoft.EntityFrameworkCore.Diagnostics;
using ToggleMesh.API.Persistence.Abstractions;

namespace ToggleMesh.API.Persistence.Interceptors;

public class UpdateAuditableInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, 
        InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        foreach (var entry in eventData.Context?.ChangeTracker.Entries<AuditableEntity>() ?? [])
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}