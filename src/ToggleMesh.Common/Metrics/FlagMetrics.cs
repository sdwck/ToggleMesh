using System.Collections.Concurrent;

namespace ToggleMesh.Common.Metrics;

public class FlagMetrics
{
    public Guid Slot0Id;
    public Guid Slot1Id;
    public long Slot0Count;
    public long Slot1Count;
    
    public ConcurrentDictionary<Guid, long>? Overflow;
    
    public void Increment(Guid variationId)
    {
        if (variationId == Slot0Id)
        {
            Interlocked.Increment(ref Slot0Count);
            return;
        }
        if (variationId == Slot1Id)
        {
            Interlocked.Increment(ref Slot1Count);
            return;
        }
        
        if (Slot0Id == Guid.Empty || Slot1Id == Guid.Empty)
        {
            lock (this)
            {
                if (Slot0Id == Guid.Empty)
                {
                    Slot0Id = variationId;
                    Interlocked.Increment(ref Slot0Count);
                    return;
                }
                if (Slot0Id == variationId)
                {
                    Interlocked.Increment(ref Slot0Count);
                    return;
                }
                
                if (Slot1Id == Guid.Empty)
                {
                    Slot1Id = variationId;
                    Interlocked.Increment(ref Slot1Count);
                    return;
                }
                if (Slot1Id == variationId)
                {
                    Interlocked.Increment(ref Slot1Count);
                    return;
                }
            }
        }
        
        var overflow = Overflow ??= new ConcurrentDictionary<Guid, long>();
        overflow.AddOrUpdate(variationId, 1, (_, count) => count + 1);
    }
    
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
        
        if (Slot0Id == Guid.Empty || Slot1Id == Guid.Empty)
        {
            lock (this)
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
            }
        }
        
        var overflow = Overflow ??= new ConcurrentDictionary<Guid, long>();
        overflow.AddOrUpdate(variationId, amount, (_, count) => count + amount);
    }
}
