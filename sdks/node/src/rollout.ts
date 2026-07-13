import { VariationWeight } from './models.js';

export function calculateFnv1aHash(flagKey: string, identity: string): number {
    const offsetBasis = 2166136261;
    const prime = 16777619;
    let hash = offsetBasis;

    for (let i = 0; i < flagKey.length; i++) {
        hash ^= flagKey.charCodeAt(i);
        hash = Math.imul(hash, prime) >>> 0;
    }

    for (let i = 0; i < identity.length; i++) {
        hash ^= identity.charCodeAt(i);
        hash = Math.imul(hash, prime) >>> 0;
    }

    return hash;
}

export function evaluateRollout(rollout: VariationWeight[] | undefined | null, flagKey: string, identity: string): string | null {
    if (!rollout || rollout.length === 0) {
        return null;
    }
    if (rollout.length === 1) {
        return rollout[0].variationId;
    }
    if (!identity) {
        return rollout[0].variationId;
    }

    const hashVal = calculateFnv1aHash(flagKey, identity);
    const bucket = hashVal % 10000;

    let sum = 0;
    for (const w of rollout) {
        sum += w.weight;
        if (bucket < sum) {
            return w.variationId;
        }
    }

    return rollout[rollout.length - 1].variationId;
}
