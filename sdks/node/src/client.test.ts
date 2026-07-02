import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ToggleMeshClient } from './client.js';
import { calculateFnv1aHash, evaluateRollout } from './rollout.js';
import { RuleEngine } from './rules.js';

describe('Node SDK Rollout Evaluator', () => {
    it('should generate consistent FNV1a hash', () => {
        const hash1 = calculateFnv1aHash("flag_user1");
        const hash2 = calculateFnv1aHash("flag_user1");
        expect(hash1).toBe(hash2);
    });

    it('should evaluate rollout correctly', () => {
        expect(evaluateRollout(undefined, "flag", "user")).toBe(true);
        expect(evaluateRollout(0, "flag", "user")).toBe(false);
        expect(evaluateRollout(100, "flag", "user")).toBe(true);

        const hash = calculateFnv1aHash("flaguser");
        const bucket = hash % 100;

        expect(evaluateRollout(bucket + 1, "flag", "user")).toBe(true);
        expect(evaluateRollout(bucket - 1, "flag", "user")).toBe(false);
    });
});

describe('Node SDK Rule Engine', () => {
    it('should compile and evaluate Equals rule', () => {
        const engine = new RuleEngine();
        const groups = engine.compileRules([
            { groupId: 1, attribute: "plan", operator: "Equals", value: "pro" }
        ]);
        expect(engine.evaluate(groups, { plan: "pro" })).toBe(true);
        expect(engine.evaluate(groups, { plan: "free" })).toBe(false);
    });

    it('should evaluate SemVer rules', () => {
        const engine = new RuleEngine();
        const groups = engine.compileRules([
            { groupId: 1, attribute: "version", operator: "SemVerGreaterThan", value: "v2.0.0" }
        ]);
        expect(engine.evaluate(groups, { version: "v2.1.0" })).toBe(true);
        expect(engine.evaluate(groups, { version: "1.9.9" })).toBe(false);
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
                        rolloutPercentage: 100,
                        rules: []
                    },
                    {
                        key: 'feature2',
                        isEnabled: true,
                        isExperimentActive: false,
                        rolloutPercentage: 100,
                        rules: [{ groupId: 1, attribute: "country", operator: "Equals", value: "US" }]
                    }
                ]
            })
        });
        globalThis.fetch = mockFetch as any;

        vi.spyOn(client as any, 'startSseLoop').mockImplementation(() => { });

        await client.start();

        expect(client.isEnabled('feature1', { identity: 'u1' })).toBe(true);
        expect(client.isEnabled('feature2', { identity: 'u1' })).toBe(false);
        expect(client.isEnabled('feature2', { identity: 'u1', context: { country: "US" } })).toBe(true);

        client.stop();
    });

    it('should track events with correct structure', async () => {
        const client = new ToggleMeshClient({
            baseUrl: 'http://localhost:5264',
            serverKey: 'test_key'
        });

        client.track('checkout', { identity: 'u123' }, { amount: 10 }, 50);

        const events = (client as any).eventsBuffer;
        expect(events).toHaveLength(1);
        expect(events[0].EventName).toBe('checkout');
        expect(events[0].Identity).toBe('u123');
        expect(events[0].Properties).toEqual({ amount: 10 });
        expect(events[0].Value).toBe(50);
        expect(events[0].Type).toBe(1);
    });
});
