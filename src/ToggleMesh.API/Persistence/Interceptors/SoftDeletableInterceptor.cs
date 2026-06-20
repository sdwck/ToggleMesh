using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ToggleMesh.API.Persistence.Abstractions;

namespace ToggleMesh.API.Persistence.Interceptors;

public class SoftDeletableInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, 
        InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        foreach (var entry in eventData.Context?.ChangeTracker.Entries<ISoftDeletable>() ?? [])
        {
            if (entry.State != EntityState.Deleted) continue;
            
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
        }
        
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}