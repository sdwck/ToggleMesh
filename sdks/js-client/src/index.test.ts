import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ToggleMeshClient } from './index.js';

describe('ToggleMeshClient SDK', () => {
    beforeEach(() => {
        vi.restoreAllMocks();
    });

    it('should initialize with correct options', () => {
        const client = new ToggleMeshClient({
            baseUrl: 'http://localhost:5264/',
            clientKey: 'tm_client_test'
        });

        expect(client.isEnabled('any-flag', false)).toBe(false);
        expect(client.isEnabled('any-flag', true)).toBe(true);
    });

    it('should fetch and evaluate flags correctly', async () => {
        const client = new ToggleMeshClient({
            baseUrl: 'http://localhost:5264',
            clientKey: 'tm_client_test'
        });

        const mockFetch = vi.fn().mockResolvedValue({
            ok: true,
            json: async () => [
                { key: 'new-checkout', isEnabled: true },
                { key: 'stale-feature', isEnabled: false }
            ]
        });
        global.fetch = mockFetch as any;

        await client.identify('user_123', { Country: 'MD' });

        expect(mockFetch).toHaveBeenCalledWith('http://localhost:5264/api/v1/sdk/evaluate', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'x-api-key': 'tm_client_test'
            },
            body: JSON.stringify({
                identity: 'user_123',
                context: { Country: 'MD' }
            })
        });

        expect(client.isEnabled('new-checkout')).toBe(true);
        expect(client.isEnabled('stale-feature')).toBe(false);
        expect(client.isEnabled('non-existent', true)).toBe(true);
    });

    it('should notify listeners when flags are updated', async () => {
        const client = new ToggleMeshClient({
            baseUrl: 'http://localhost:5264',
            clientKey: 'tm_client_test'
        });

        global.fetch = vi.fn().mockResolvedValue({
            ok: true,
            json: async () => [{ key: 'feature', isEnabled: true }]
        }) as any;

        let callbackCalled = false;
        let receivedFlags: Record<string, boolean> = {};

        const unsubscribe = client.subscribe((flags) => {
            callbackCalled = true;
            receivedFlags = flags;
        });

        await client.identify('user_123');

        expect(callbackCalled).toBe(true);
        expect(receivedFlags['feature']).toBe(true);

        unsubscribe();
        callbackCalled = false;

        await client.identify('user_123');
        expect(callbackCalled).toBe(false);
    });
});