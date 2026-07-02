using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Infrastructure.Data.Interceptors;

public class UpdateAuditableInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;

    public UpdateAuditableInterceptor(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, 
        InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        
        foreach (var entry in eventData.Context?.ChangeTracker.Entries<AuditableEntity>() ?? [])
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}