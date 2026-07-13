import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ToggleMeshClient } from './index.js';

const BASE_URL = 'http://localhost:5264';

describe('ToggleMeshClient SDK', () => {
    beforeEach(() => {
        vi.restoreAllMocks();
    });

    it('should initialize with correct options', () => {
        const client = new ToggleMeshClient({
            baseUrl: `${BASE_URL}/`,
            clientKey: 'tm_client_test'
        });

        expect(client.isEnabled('any-flag', false)).toBe(false);
        expect(client.isEnabled('any-flag', true)).toBe(true);
    });

    it('should fetch and evaluate flags correctly', async () => {
        const client = new ToggleMeshClient({
            baseUrl: BASE_URL,
            clientKey: 'tm_client_test'
        });

        const mockFetch = vi.fn().mockImplementation(() => Promise.resolve({
            ok: true,
            json: () => Promise.resolve([
                { key: 'new-checkout', variationId: 'v1', variationValue: 'true' },
                { key: 'stale-feature', variationId: 'v2', variationValue: 'false' }
            ])
        }));
        global.fetch = mockFetch as any;

        await client.identify('user_123', { Country: 'MD' });

        expect(mockFetch).toHaveBeenCalledWith(`${BASE_URL}/api/v1/sdk/evaluate`, {
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
            baseUrl: BASE_URL,
            clientKey: 'tm_client_test'
        });

        const mockFetch = vi.fn().mockImplementation(() => Promise.resolve({
            ok: true,
            json: () => Promise.resolve([
                { key: 'feature', variationId: 'v1', variationValue: 'true' }
            ])
        }));
        global.fetch = mockFetch as any;
        
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

    describe('Evaluation Types', () => {
        let evalClient: ToggleMeshClient;

        beforeEach(async () => {
            evalClient = new ToggleMeshClient({
                baseUrl: BASE_URL,
                clientKey: 'tm_client_test'
            });

            const mockFetch = vi.fn().mockImplementation(() => Promise.resolve({
                ok: true,
                json: () => Promise.resolve([
                    { key: 'str-flag', variationId: 'v1', variationValue: 'hello world' },
                    { key: 'bool-flag', variationId: 'v2', variationValue: 'true' },
                    { key: 'num-flag', variationId: 'v3', variationValue: '42.5' },
                    { key: 'bad-num-flag', variationId: 'v4', variationValue: 'not a number' },
                    { key: 'json-flag', variationId: 'v5', variationValue: '{"theme":"dark"}' },
                    { key: 'bad-json-flag', variationId: 'v6', variationValue: 'invalid { json' }
                ])
            }));
            global.fetch = mockFetch as any;

            await evalClient.identify('user_1');
        });

        it('should get string variation', () => {
            expect(evalClient.getVariation('str-flag', 'default')).toBe('hello world');
            expect(evalClient.getVariation('missing-flag', 'default')).toBe('default');
        });

        it('should get boolean variation', () => {
            expect(evalClient.isEnabled('bool-flag', false)).toBe(true);
            expect(evalClient.isEnabled('missing-flag', true)).toBe(true);
        });

        it('should get number variation', () => {
            expect(evalClient.getNumber('num-flag', 0)).toBe(42.5);
            expect(evalClient.getNumber('bad-num-flag', 99)).toBe(99);
            expect(evalClient.getNumber('missing-flag', 10)).toBe(10);
        });

        it('should get json variation', () => {
            expect(evalClient.getJson('json-flag', {})).toEqual({ theme: 'dark' });
            expect(evalClient.getJson('bad-json-flag', { fallback: true })).toEqual({ fallback: true });
            expect(evalClient.getJson('missing-flag', { fallback: true })).toEqual({ fallback: true });
        });
    });

    describe('track()', () => {
        it('should not track without identity', () => {
            const client = new ToggleMeshClient({
                baseUrl: BASE_URL,
                clientKey: 'tm_client_test'
            });

            const mockFetch = vi.fn().mockResolvedValue({ ok: true });
            global.fetch = mockFetch as any;

            client.track('purchase');

            expect(mockFetch).not.toHaveBeenCalled();
        });

        it('should buffer events and flush when threshold is reached', async () => {
            const client = new ToggleMeshClient({
                baseUrl: BASE_URL,
                clientKey: 'tm_client_test'
            });

            const mockFetch = vi.fn().mockResolvedValue({
                ok: true,
                json: async () => []
            });
            global.fetch = mockFetch as any;

            await client.identify('user_42');
            mockFetch.mockClear();

            for (let i = 0; i < 19; i++) {
                client.track(`event_${i}`);
            }
            expect(mockFetch).not.toHaveBeenCalled();

            client.track('event_19');

            expect(mockFetch).toHaveBeenCalledTimes(1);
            const [url, options] = mockFetch.mock.calls[0];
            expect(url).toBe(`${BASE_URL}/api/v1/sdk/events`);
            expect(options.method).toBe('POST');

            const body = JSON.parse(options.body);
            expect(body.Events).toHaveLength(20);
            expect(body.Events[0].Identity).toBe('user_42');
            expect(body.Events[0].Type).toBe(1);
            expect(body.Events[0].EventName).toBe('event_0');
        });

        it('should include properties and value in tracked events', async () => {
            const client = new ToggleMeshClient({
                baseUrl: BASE_URL,
                clientKey: 'tm_client_test'
            });

            const mockFetch = vi.fn().mockResolvedValue({
                ok: true,
                json: async () => []
            });
            global.fetch = mockFetch as any;

            await client.identify('user_99');
            mockFetch.mockClear();

            client.track('purchase', { item: 'sword' }, 49.99);

            for (let i = 0; i < 19; i++) {
                client.track(`filler_${i}`);
            }

            const body = JSON.parse(mockFetch.mock.calls[0][1].body);
            const purchaseEvent = body.Events[0];
            expect(purchaseEvent.EventName).toBe('purchase');
            expect(purchaseEvent.Properties).toEqual({ item: 'sword' });
            expect(purchaseEvent.Value).toBe(49.99);
        });

        it('should flush remaining events on clearIdentity', async () => {
            const client = new ToggleMeshClient({
                baseUrl: BASE_URL,
                clientKey: 'tm_client_test'
            });

            const mockFetch = vi.fn().mockResolvedValue({
                ok: true,
                json: async () => []
            });
            global.fetch = mockFetch as any;

            await client.identify('user_1');
            mockFetch.mockClear();

            client.track('page_view');
            client.track('click');

            client.clearIdentity();

            expect(mockFetch).toHaveBeenCalledTimes(1);
            const body = JSON.parse(mockFetch.mock.calls[0][1].body);
            expect(body.Events).toHaveLength(2);
        });

        it('should send Events payload with correct wrapper format', async () => {
            const client = new ToggleMeshClient({
                baseUrl: BASE_URL,
                clientKey: 'tm_client_test'
            });

            const mockFetch = vi.fn().mockResolvedValue({
                ok: true,
                json: async () => []
            });
            global.fetch = mockFetch as any;

            await client.identify('user_x');
            mockFetch.mockClear();

            client.track('signup');
            client.clearIdentity();

            const body = JSON.parse(mockFetch.mock.calls[0][1].body);
            expect(body).toHaveProperty('Events');
            expect(Array.isArray(body.Events)).toBe(true);
        });
    });
});