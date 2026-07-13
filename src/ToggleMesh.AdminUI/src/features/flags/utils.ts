import type { VariationWeight } from "@/api/types";

export function getDefaultRollout(variations: { id: string; value: string }[]): VariationWeight[] {
    if (!variations || variations.length === 0) return [];

    return variations.map((v, index) => {
        return {
            variationId: v.id,
            weight: index === 0 ? 100 : 0
        };
    });
}
