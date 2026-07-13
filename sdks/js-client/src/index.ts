export interface ToggleMeshOptions {
    baseUrl: string;
    clientKey: string;
    refreshInterval?: number;
    isMetricsEnabled?: boolean;
    analyticsChannelCapacity?: number;
    metricsBufferCapacity?: number;
    maxBatchSize?: number;
}

export interface FlagState {
    key: string;
    variationId?: string;
    variationValue?: string;
    isExperimentActive?: boolean;
}

export type ToggleMeshListener = (flags: Record<string, boolean>) => void;

export class ToggleMeshClient {
    private baseUrl: string;
    private clientKey: string;
    private refreshInterval: number;
    private flags: Record<string, FlagState> = {};
    private activeExperiments = new Set<string>();
    private listeners = new Set<ToggleMeshListener>();

    private currentIdentity: string = '';
    private currentContext: Record<string, string> = {};
    private intervalId: any = null;
    private eventBuffer: any[] = [];
    private metricsBuffer = new Map<string, { variationsCount: Record<string, number> }>();
    private flushIntervalId: any = null;
    private readonly EVENT_FLUSH_INTERVAL = 10000;
    private readonly supportsKeepalive: boolean = false;

    private isMetricsEnabled: boolean;
    private analyticsChannelCapacity: number;
    private metricsBufferCapacity: number;
    private maxBatchSize: number;

    constructor(options: ToggleMeshOptions) {
        this.baseUrl = options.baseUrl.replace(/\/$/, '');
        this.clientKey = options.clientKey;
        this.refreshInterval = options.refreshInterval !== undefined ? options.refreshInterval : 60;
        this.isMetricsEnabled = options.isMetricsEnabled !== false;
        this.analyticsChannelCapacity = options.analyticsChannelCapacity || 10000;
        this.metricsBufferCapacity = options.metricsBufferCapacity || 10000;
        this.maxBatchSize = options.maxBatchSize || 20;

        this.startEventFlushTimer();

        if (typeof window !== 'undefined') {
            this.supportsKeepalive = 'keepalive' in Request.prototype;
            window.addEventListener('beforeunload', () => {
                if (this.eventBuffer.length > 0) {
                    const payload = JSON.stringify({ Events: [...this.eventBuffer] });
                    this.eventBuffer = [];
                    if (navigator.sendBeacon) {
                        const blob = new Blob([payload], { type: 'application/json' });
                        navigator.sendBeacon(`${this.baseUrl}/api/v1/sdk/events?x-api-key=${this.clientKey}`, blob);
                    }
                }
                
                const metricPayload: any[] = [];
                for (const [key, m] of this.metricsBuffer.entries()) {
                    if (Object.keys(m.variationsCount).length > 0) {
                        metricPayload.push({ Key: key, VariationsCount: { ...m.variationsCount } });
                    }
                }
                if (metricPayload.length > 0 && navigator.sendBeacon) {
                    const blob = new Blob([JSON.stringify(metricPayload)], { type: 'application/json' });
                    navigator.sendBeacon(`${this.baseUrl}/api/v1/sdk/metrics?x-api-key=${this.clientKey}`, blob);
                }
            });
        }
    }

    async identify(identity: string, context: Record<string, string> = {}): Promise<void> {
        this.currentIdentity = identity;
        this.currentContext = context;

        await this.fetchFlagsAsync();

        if (this.refreshInterval > 0) {
            this.stopPolling();
            this.startPolling();
        }
    }

    getVariation(flagKey: string, defaultValue = ""): string {
        const flag = this.flags[flagKey];
        if (!flag) return defaultValue;

        const val = flag.variationValue ?? defaultValue;

        if (this.isMetricsEnabled && flag.variationId) {
            if (!this.metricsBuffer.has(flagKey)) {
                if (this.metricsBuffer.size < this.metricsBufferCapacity) {
                    this.metricsBuffer.set(flagKey, { variationsCount: {} });
                }
            }
            const m = this.metricsBuffer.get(flagKey);
            if (m) {
                m.variationsCount[flag.variationId] = (m.variationsCount[flag.variationId] || 0) + 1;
            }
        }

        if (this.isMetricsEnabled && this.currentIdentity && this.activeExperiments.has(flagKey) && flag.variationId) {
            if (this.eventBuffer.length < this.analyticsChannelCapacity) {
                this.eventBuffer.push({
                    Type: 0,
                    Timestamp: Date.now(),
                    Identity: this.currentIdentity,
                    FlagKey: flagKey,
                    Properties: this.currentContext,
                    VariationId: flag.variationId
                });

                if (this.eventBuffer.length >= this.maxBatchSize) {
                    this.flushEvents();
                }
            }
        }

        return val;
    }

    isEnabled(flagKey: string, defaultValue = false): boolean {
        const str = this.getVariation(flagKey, defaultValue ? "true" : "false");
        return str.toLowerCase() === "true";
    }

    getNumber(flagKey: string, defaultValue = 0): number {
        const str = this.getVariation(flagKey, "");
        if (!str) return defaultValue;
        const parsed = parseFloat(str);
        return isNaN(parsed) ? defaultValue : parsed;
    }

    getJson<T>(flagKey: string, defaultValue: T): T {
        const str = this.getVariation(flagKey, "");
        if (!str) return defaultValue;
        try {
            return JSON.parse(str) as T;
        } catch {
            return defaultValue;
        }
    }

    track(eventName: string, properties?: Record<string, any>, value?: number): void {
        if (!this.isMetricsEnabled || !this.currentIdentity || !eventName) return;
        if (this.eventBuffer.length >= this.analyticsChannelCapacity) return;

        this.eventBuffer.push({
            Type: 1,
            Timestamp: Date.now(),
            Identity: this.currentIdentity,
            EventName: eventName,
            Properties: properties,
            Value: value
        });

        if (this.eventBuffer.length >= this.maxBatchSize) {
            this.flushEvents();
        }
    }

    clearIdentity(): void {
        this.flushEvents();
        this.flags = {};
        this.currentIdentity = '';
        this.currentContext = {};
        this.stopPolling();
        this.notifyListeners();
    }

    subscribe(listener: ToggleMeshListener): () => void {
        this.listeners.add(listener);
        return () => this.listeners.delete(listener);
    }

    private startPolling(): void {
        this.intervalId = setInterval(async () => {
            await this.fetchFlagsAsync();
        }, this.refreshInterval * 1000);
    }

    private stopPolling(): void {
        if (this.intervalId) {
            clearInterval(this.intervalId);
            this.intervalId = null;
        }
    }

    private startEventFlushTimer(): void {
        this.flushIntervalId = setInterval(() => {
            this.flushEvents();
            this.flushMetrics();
        }, this.EVENT_FLUSH_INTERVAL);
    }

    private async flushMetrics(): Promise<void> {
        if (this.metricsBuffer.size === 0) return;

        const payload: any[] = [];
        for (const [key, m] of this.metricsBuffer.entries()) {
            if (Object.keys(m.variationsCount).length > 0) {
                payload.push({ Key: key, VariationsCount: { ...m.variationsCount } });
                m.variationsCount = {};
            }
        }

        if (payload.length === 0) return;

        try {
            const fetchOptions: RequestInit = {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'x-api-key': this.clientKey
                },
                body: JSON.stringify(payload)
            };

            if (this.supportsKeepalive) {
                fetchOptions.keepalive = true;
            }

            const response = await fetch(`${this.baseUrl}/api/v1/sdk/metrics`, fetchOptions);

            if (!response.ok) {
                console.warn(`[ToggleMesh] Failed to flush metrics: ${response.status}`);
            }
        } catch (error) {
            console.error('[ToggleMesh] Network error during metrics flush.', error);
        }
    }

    private async flushEvents(): Promise<void> {
        if (this.eventBuffer.length === 0) return;

        const eventsToSend = this.eventBuffer.splice(0, this.maxBatchSize);

        try {
            const fetchOptions: RequestInit = {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'x-api-key': this.clientKey
                },
                body: JSON.stringify({ Events: eventsToSend })
            };

            if (this.supportsKeepalive) {
                fetchOptions.keepalive = true;
            }

            const response = await fetch(`${this.baseUrl}/api/v1/sdk/events`, fetchOptions);

            if (!response.ok) {
                console.warn(`[ToggleMesh] Failed to flush events: ${response.status}`);
            }
        } catch (error) {
            console.error('[ToggleMesh] Network error during event flush.', error);
        }
    }

    private async fetchFlagsAsync(): Promise<void> {
        try {
            const response = await fetch(`${this.baseUrl}/api/v1/sdk/evaluate`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'x-api-key': this.clientKey
                },
                body: JSON.stringify({
                    identity: this.currentIdentity,
                    context: this.currentContext
                })
            });

            if (!response.ok) {
                console.warn(`[ToggleMesh] Evaluation failed: ${response.status}`);
                return;
            }

            const data: FlagState[] = await response.json();

            this.activeExperiments.clear();

            this.flags = data.reduce((acc, flag) => {
                acc[flag.key] = flag;
                if (flag.isExperimentActive) {
                    this.activeExperiments.add(flag.key);
                }
                return acc;
            }, {} as Record<string, FlagState>);

            this.notifyListeners();
        } catch (error) {
            console.error('[ToggleMesh] Network error during background sync.', error);
        }
    }

    private notifyListeners(): void {
        const boolFlags: Record<string, boolean> = {};
        for (const [key, val] of Object.entries(this.flags)) {
            boolFlags[key] = val.variationValue?.toLowerCase() === "true";
        }
        this.listeners.forEach(listener => listener(boolFlags));
    }
}