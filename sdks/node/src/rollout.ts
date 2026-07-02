export function calculateFnv1aHash(text: string): number {
    const offsetBasis = 2166136261;
    const prime = 16777619;
    let hash = offsetBasis;
    
    const encoder = new TextEncoder();
    const bytes = encoder.encode(text);
    
    for (let i = 0; i < bytes.length; i++) {
        hash ^= bytes[i];
        hash = Math.imul(hash, prime) >>> 0;
    }
    
    return hash;
}

export function evaluateRollout(rolloutPercentage: number | undefined | null, flagKey: string, identity: string): boolean {
    if (rolloutPercentage === undefined || rolloutPercentage === null) {
        return true;
    }
    if (rolloutPercentage <= 0) {
        return false;
    }
    if (rolloutPercentage >= 100) {
        return true;
    }
    if (!identity) {
        return false;
    }
    
    const hashVal = calculateFnv1aHash(flagKey + identity);
    const bucket = hashVal % 100;
    return bucket < rolloutPercentage;
}
