import { z } from 'zod';

export const ruleSchema = z.object({
    priority: z.number().default(0),
    groupId: z.number(),
    attribute: z.string().min(1, 'Attribute is required'),
    operator: z.string().min(1, 'Operator is required'),
    value: z.string().min(1, 'Value is required'),
    rollout: z.array(z.object({
        variationId: z.string(),
        weight: z.number()
    })).default([]),
});
