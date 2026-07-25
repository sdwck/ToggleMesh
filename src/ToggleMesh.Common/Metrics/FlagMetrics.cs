using System.Collections.Concurrent;

namespace ToggleMesh.Common.Metrics;

public class FlagMetrics
{
    private readonly Lock _lock = new();

    public Guid Slot0Id;
    public Guid Slot1Id;
    public long Slot0Count;
    public long Slot1Count;
    
    public ConcurrentDictionary<Guid, long>? Overflow;
    
    public void Increment(Guid variationId) => AddCount(variationId, 1);
    
    public void AddCount(Guid variationId, long amount)
    {
        if (variationId == Slot0Id)
        {
            Interlocked.Add(ref Slot0Count, amount);
            return;
        }
        if (variationId == Slot1Id)
        {
            Interlocked.Add(ref Slot1Count, amount);
            return;
        }
        
        AddCountSlow(variationId, amount);
    }

    private void AddCountSlow(Guid variationId, long amount)
    {
        lock (_lock)
        {
            if (Slot0Id == Guid.Empty)
            {
                Slot0Id = variationId;
                Interlocked.Add(ref Slot0Count, amount);
                return;
            }
            if (Slot0Id == variationId)
            {
                Interlocked.Add(ref Slot0Count, amount);
                return;
            }
            
            if (Slot1Id == Guid.Empty)
            {
                Slot1Id = variationId;
                Interlocked.Add(ref Slot1Count, amount);
                return;
            }
            if (Slot1Id == variationId)
            {
                Interlocked.Add(ref Slot1Count, amount);
                return;
            }

            Overflow ??= new ConcurrentDictionary<Guid, long>();
        }
        
        Overflow.AddOrUpdate(
            variationId, 
            static (k, a) => a, 
            static (k, count, a) => count + a, 
            amount);
    }
}
