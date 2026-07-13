import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ToggleMeshClient } from './client.js';
import { calculateFnv1aHash, evaluateRollout } from './rollout.js';
import { RuleEngine } from './rules.js';

describe('Node SDK Rollout Evaluator', () => {
    it('should generate consistent FNV1a hash', () => {
        const hash1 = calculateFnv1aHash("flag", "_user1");
        const hash2 = calculateFnv1aHash("flag", "_user1");
        expect(hash1).toBe(hash2);
    });

    it('should evaluate rollout correctly', () => {
        expect(evaluateRollout(undefined, "flag", "user")).toBe(null);
        expect(evaluateRollout([], "flag", "user")).toBe(null);
        expect(evaluateRollout([{ variationId: "v1", weight: 10000 }], "flag", "user")).toBe("v1");

        const hash = calculateFnv1aHash("flag", "user");
        const bucket = hash % 10000;

        expect(evaluateRollout([{ variationId: "v1", weight: bucket + 1 }, { variationId: "v2", weight: 10000 - (bucket + 1) }], "flag", "user")).toBe("v1");
        expect(evaluateRollout([{ variationId: "v1", weight: bucket }, { variationId: "v2", weight: 10000 - bucket }], "flag", "user")).toBe("v2");
    });
});

describe('Node SDK Rule Engine', () => {
    it('should compile and evaluate Equals rule', () => {
        const engine = new RuleEngine();
        const groups = engine.compileRules([
            { priority: 0, groupId: 1, attribute: "plan", operator: "Equals", value: "pro", rollout: [] }
        ]);
        expect(engine.evaluate(groups, { plan: "pro" })).toBeTruthy();
        expect(engine.evaluate(groups, { plan: "free" })).toBeNull();
    });

    it('should evaluate SemVer rules', () => {
        const engine = new RuleEngine();
        const groups = engine.compileRules([
            { priority: 0, groupId: 1, attribute: "version", operator: "SemVerGreaterThan", value: "v2.0.0", rollout: [] }
        ]);
        expect(engine.evaluate(groups, { version: "v2.1.0" })).toBeTruthy();
        expect(engine.evaluate(groups, { version: "1.9.9" })).toBeNull();
    });
});

describe('ToggleMeshClient Node', () => {
    beforeEach(() => {
        vi.restoreAllMocks();
    });

    it('should sync state and evaluate flags', async () => {
        const client = new ToggleMeshClient({
            baseUrl: 'http://localhost:5264',
            serverKey: 'test_key'
        });

        const mockFetch = vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({
                flags: [
                    {
                        key: 'feature1',
                        isEnabled: true,
                        isExperimentActive: false,
                        fallthroughRollout: [{ variationId: "v_true", weight: 10000 }],
                        variations: { "v_true": "true", "v_false": "false" },
                        rules: []
                    },
                    {
                        key: 'feature2',
                        isEnabled: true,
                        isExperimentActive: false,
                        fallthroughRollout: [{ variationId: "v_false", weight: 10000 }],
                        variations: { "v_true": "true", "v_false": "false" },
                        rules: [{ priority: 0, groupId: 1, attribute: "country", operator: "Equals", value: "US", rollout: [{ variationId: "v_true", weight: 10000 }] }]
                    }
                ]
            })
        });
        globalThis.fetch = mockFetch as any;

        vi.spyOn(client as any, 'startSseLoop').mockImplementation(() => { });

        await client.start();

        expect(client.isEnabled('feature1', false, { identity: 'u1' })).toBe(true);
        expect(client.isEnabled('feature2', false, { identity: 'u1' })).toBe(false);
        expect(client.isEnabled('feature2', false, { identity: 'u1', context: { country: "US" } })).toBe(true);

        client.stop();
    });

    it('should track events with correct structure', async () => {
        const client = new ToggleMeshClient({
            baseUrl: 'http://localhost:5264',
            serverKey: 'test_key'
        });

        client.track('checkout', { identity: 'u123', context: { amount: 10 }, value: 50 });

        const events = (client as any).eventsBuffer;
        expect(events).toHaveLength(1);
        expect(events[0].EventName).toBe('checkout');
        expect(events[0].Identity).toBe('u123');
        expect(events[0].Properties).toEqual({ amount: 10 });
        expect(events[0].Value).toBe(50);
        expect(events[0].Type).toBe(1);
    });
});
