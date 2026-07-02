import { ToggleMeshOptions, ToggleMeshUser, SdkGetFlagsResponse, FeatureFlagDto, SegmentDto, RuleDto } from './models.js';
import { RuleEngine, CachedFlag, CachedSegment, ISegmentProvider, CompiledRuleGroup } from './rules.js';
import { evaluateRollout } from './rollout.js';

class FlagMetrics {
    trueCount = 0;
    falseCount = 0;
}

export class ToggleMeshClient implements ISegmentProvider {
    private options: ToggleMeshOptions;
    private baseUrl: string;

    private flagsCache = new Map<string, CachedFlag>();
    private segmentsCache = new Map<string, CachedSegment>();

    private ruleEngine: RuleEngine;

    private metricsBuffer = new Map<string, FlagMetrics>();
    private eventsBuffer: any[] = [];

    private pollingAbortController: AbortController | null = null;
    private flushIntervalId: any = null;

    private isRunning = false;

    private analyticsChannelCapacity: number;
    private metricsBufferCapacity: number;
    private maxBatchSize: number;

    constructor(options: ToggleMeshOptions) {
        this.options = options;
        this.baseUrl = options.baseUrl.replace(/\/$/, '');
        this.ruleEngine = new RuleEngine(this);
        this.analyticsChannelCapacity = options.analyticsChannelCapacity || 10000;
        this.metricsBufferCapacity = options.metricsBufferCapacity || 10000;
        this.maxBatchSize = options.maxBatchSize || 2000;
    }

    getSegmentRules(segmentId: string): CompiledRuleGroup[] | null {
        const segment = this.segmentsCache.get(segmentId);
        return segment ? segment.groups : null;
    }

    async start(): Promise<void> {
        if (this.isRunning) return;
        this.isRunning = true;

        await this.syncState();

        this.startSseLoop();

        this.flushIntervalId = setInterval(() => {
            this.flushMetrics();
            this.flushEvents();
        }, 10000);
    }

    stop(): void {
        this.isRunning = false;
        if (this.pollingAbortController) {
            this.pollingAbortController.abort();
            this.pollingAbortController = null;
        }
        if (this.flushIntervalId) {
            clearInterval(this.flushIntervalId);
            this.flushIntervalId = null;
        }

        this.flushMetrics().catch(() => { });
        this.flushEvents().catch(() => { });
    }

    private async syncState(): Promise<void> {
        try {
            const response = await fetch(`${this.baseUrl}/api/v1/sdk/flags`, {
                headers: { 'x-api-key': this.options.serverKey }
            });

            if (!response.ok) {
                console.warn(`[ToggleMesh Node] Failed to sync state: ${response.status}`);
                return;
            }

            const data = await response.json() as SdkGetFlagsResponse;

            this.flagsCache.clear();
            this.segmentsCache.clear();

            for (const fData of data.flags || []) {
                this.cacheFlag(fData);
            }
            for (const sData of data.segments || []) {
                this.cacheSegment(sData);
            }
        } catch (error) {
            console.error(`[ToggleMesh Node] Error syncing state:`, error);
        }
    }

    private cacheFlag(dto: FeatureFlagDto) {
        const groups = this.ruleEngine.compileRules(dto.rules || []);
        this.flagsCache.set(dto.key, new CachedFlag(dto, groups));
    }

    private cacheSegment(dto: SegmentDto) {
        const groups = this.ruleEngine.compileRules(dto.rules || []);
        this.segmentsCache.set(dto.id, new CachedSegment(dto, groups));
    }

    private async startSseLoop() {
        this.pollingAbortController = new AbortController();
        let backoff = 1000;

        while (this.isRunning) {
            try {
                const response = await fetch(`${this.baseUrl}/api/v1/stream`, {
                    headers: { 'x-api-key': this.options.serverKey, 'Accept': 'text/event-stream' },
                    signal: this.pollingAbortController.signal
                });

                if (response.status === 401) {
                    console.error("[ToggleMesh Node] Invalid API Key. Stopping stream.");
                    this.stop();
                    break;
                }

                if (!response.ok || !response.body) {
                    throw new Error(`Bad status ${response.status}`);
                }

                backoff = 1000;
                const reader = response.body.getReader();
                const decoder = new TextDecoder();
                let buffer = "";

                while (this.isRunning) {
                    const { done, value } = await reader.read();
                    if (done) break;

                    buffer += decoder.decode(value, { stream: true });
                    const lines = buffer.split('\n');
                    buffer = lines.pop() || "";

                    for (let line of lines) {
                        line = line.trim();
                        if (line.startsWith("data: ")) {
                            this.handleSseEvent(line.substring(6));
                        }
                    }
                }
            } catch (err: any) {
                if (err.name === 'AbortError') break;
                console.debug(`[ToggleMesh Node] SSE connection lost. Reconnecting in ${backoff}ms...`);
            }

            if (!this.isRunning) break;

            const jitter = Math.random() * 1000;
            const waitTime = backoff + jitter;
            await new Promise(r => setTimeout(r, waitTime));
            backoff = Math.min(backoff * 2, 30000);

            if (this.isRunning) {
                await this.syncState();
            }
        }
    }

    private toCamelCase(obj: any): any {
        if (Array.isArray(obj)) {
            return obj.map(v => this.toCamelCase(v));
        } else if (obj !== null && typeof obj === 'object') {
            return Object.keys(obj).reduce((result: any, key: string) => {
                const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
                result[camelKey] = this.toCamelCase(obj[key]);
                return result;
            }, {});
        }
        return obj;
    }

    private handleSseEvent(data: string) {
        try {
            const doc = JSON.parse(data);
            const eventName = doc.EventName || doc.eventName;

            if (eventName === "FlagUpdated" && doc.Payload) {
                let payload = doc.Payload;
                if (typeof payload === 'string') payload = JSON.parse(payload);
                payload = this.toCamelCase(payload);
                this.cacheFlag(payload as FeatureFlagDto);
            } else if (eventName === "StateReloadRequired") {
                this.syncState();
            }
        } catch (e) {
            console.error(`[ToggleMesh Node] Failed to parse SSE event`, e);
        }
    }

    isEnabled(flagKey: string, user: ToggleMeshUser, defaultValue: boolean = false): boolean {
        const flag = this.flagsCache.get(flagKey);

        if (!flag) {
            return defaultValue;
        }

        const context = user.context || {};
        let activeRolloutPercentage = flag.rolloutPercentage;

        if (Object.keys(flag.parsedContextualRollouts).length > 0 && flag.originalDto.contextPartitionKeys) {
            const parts = [];
            for (const key of flag.originalDto.contextPartitionKeys) {
                parts.push(String(context[key] ?? "null"));
            }
            const sliceKey = parts.join("|");
            if (sliceKey in flag.parsedContextualRollouts) {
                activeRolloutPercentage = flag.parsedContextualRollouts[sliceKey];
            }
        }

        let result = false;
        if (flag.isEnabled && this.ruleEngine.evaluate(flag.groups, context)) {
            result = evaluateRollout(activeRolloutPercentage, flagKey, user.identity);
        }

        this.updateMetrics(flagKey, result);

        if (user.identity && flag.isExperimentActive) {
            this.queueEvent(0, user.identity, flagKey, result, undefined, context, undefined);
        }

        return result;
    }

    track(eventName: string, user: ToggleMeshUser, properties?: any, value?: number): void {
        if (!user.identity || !eventName) return;
        this.queueEvent(1, user.identity, undefined, undefined, eventName, properties, value);
    }

    private updateMetrics(flagKey: string, result: boolean) {
        if (this.options.isMetricsEnabled === false) return;

        let m = this.metricsBuffer.get(flagKey);
        if (!m) {
            if (this.metricsBuffer.size >= this.metricsBufferCapacity) return;
            m = new FlagMetrics();
            this.metricsBuffer.set(flagKey, m);
        }
        if (result) m.trueCount++;
        else m.falseCount++;
    }

    private queueEvent(type: number, identity: string, flagKey?: string, result?: boolean, eventName?: string, properties?: any, value?: number) {
        if (this.options.isMetricsEnabled === false) return;
        if (this.eventsBuffer.length >= this.analyticsChannelCapacity) return;

        const evt: any = {
            Type: type,
            Timestamp: Date.now(),
            Identity: identity,
            Properties: properties
        };
        if (flagKey !== undefined) evt.FlagKey = flagKey;
        if (result !== undefined) evt.Result = result;
        if (eventName !== undefined) evt.EventName = eventName;
        if (value !== undefined) evt.Value = value;

        this.eventsBuffer.push(evt);
    }

    private async flushMetrics() {
        if (this.metricsBuffer.size === 0) return;

        const payload: any[] = [];
        for (const [key, m] of this.metricsBuffer.entries()) {
            if (m.trueCount > 0 || m.falseCount > 0) {
                payload.push({ Key: key, TrueCount: m.trueCount, FalseCount: m.falseCount });
                m.trueCount = 0;
                m.falseCount = 0;
            }
        }

        if (payload.length === 0) return;

        try {
            const response = await fetch(`${this.baseUrl}/api/v1/sdk/metrics`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'x-api-key': this.options.serverKey },
                body: JSON.stringify(payload)
            });
            if (!response.ok) {
                console.error(`[ToggleMesh Node] Failed to flush metrics: ${response.status} - ${await response.text()}`);
                throw new Error("Failed to flush");
            }
        } catch (e) {
            for (const item of payload) {
                let m = this.metricsBuffer.get(item.Key);
                if (!m) { m = new FlagMetrics(); this.metricsBuffer.set(item.Key, m); }
                m.trueCount += item.TrueCount;
                m.falseCount += item.FalseCount;
            }
        }
    }

    private async flushEvents() {
        if (this.eventsBuffer.length === 0) return;

        const batch = this.eventsBuffer.splice(0, this.maxBatchSize);

        try {
            const response = await fetch(`${this.baseUrl}/api/v1/sdk/events`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'x-api-key': this.options.serverKey },
                body: JSON.stringify({ Events: batch })
            });
            if (!response.ok) {
                console.error(`[ToggleMesh Node] Failed to flush events: ${response.status} - ${await response.text()}`);
                if (this.eventsBuffer.length < this.analyticsChannelCapacity) this.eventsBuffer.unshift(...batch);
            }
        } catch (e) {
            if (this.eventsBuffer.length < this.analyticsChannelCapacity) {
                this.eventsBuffer.unshift(...batch);
            }
        }
    }
}
