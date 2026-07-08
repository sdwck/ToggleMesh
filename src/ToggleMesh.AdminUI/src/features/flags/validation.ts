import { z } from 'zod';

export const ruleSchema = z.object({
    groupId: z.number(),
    attribute: z.string().min(1, 'Attribute is required'),
    operator: z.string().min(1, 'Operator is required'),
    value: z.string().min(1, 'Value is required'),
});
