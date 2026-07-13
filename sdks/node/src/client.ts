import { ToggleMeshOptions, ToggleMeshUser, SdkGetFlagsResponse, FeatureFlagDto, SegmentDto, RuleDto, EvalOptions, TrackOptions } from './models.js';
import { RuleEngine, CachedFlag, CachedSegment, ISegmentProvider, CompiledRuleGroup } from './rules.js';
import { evaluateRollout } from './rollout.js';

class FlagMetrics {
    variationsCount: Record<string, number> = {};
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

            if (eventName === "SdkFlagUpdated" && doc.Payload) {
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

    private getVariationInternal(flagKey: string, identity: string, context?: Record<string, any>, defaultValue: string = ""): string {
        const flag = this.flagsCache.get(flagKey);

        if (!flag) {
            return defaultValue;
        }

        context = context || {};
        
        let variationId: string | null = null;
        if (!flag.isEnabled) {
            variationId = flag.originalDto.offVariationId || null;
        } else {
            if (flag.originalDto.individualTargets && identity) {
                if (flag.originalDto.individualTargets[identity]) {
                    variationId = flag.originalDto.individualTargets[identity];
                }
            }

            if (!variationId) {
                let activeRollout = flag.originalDto.fallthroughRollout;
                
                if (Object.keys(flag.parsedContextualRollouts).length > 0 && flag.originalDto.contextPartitionKeys) {
                    let sliceKey = "";
                    for (let i = 0; i < flag.originalDto.contextPartitionKeys.length; i++) {
                        if (i > 0) sliceKey += "|";
                        sliceKey += String(context[flag.originalDto.contextPartitionKeys[i]] ?? "null");
                    }
                    if (sliceKey in flag.parsedContextualRollouts) {
                        activeRollout = flag.parsedContextualRollouts[sliceKey];
                    }
                }

                if (flag.groups.length === 0) {
                    variationId = evaluateRollout(activeRollout, flagKey, identity);
                } else {
                    const matchedGroup = this.ruleEngine.evaluate(flag.groups, context);
                    if (matchedGroup) {
                        variationId = evaluateRollout(matchedGroup.rollout, flagKey, identity);
                    } else {
                        variationId = evaluateRollout(activeRollout, flagKey, identity);
                    }
                }
            }
        }
        
        if (variationId) {
            this.updateMetrics(flagKey, variationId);

            if (identity && flag.isExperimentActive) {
                this.queueEvent(0, identity, flagKey, undefined, undefined, context, undefined, variationId);
            }
            
            if (flag.originalDto.variations[variationId] !== undefined) {
                return flag.originalDto.variations[variationId] ?? defaultValue;
            }
        }

        return defaultValue;
    }

    getStringValue(flagKey: string, defaultValue: string, options?: EvalOptions): string {
        return this.getVariationInternal(flagKey, options?.identity || "", options?.context, defaultValue);
    }

    isEnabled(flagKey: string, defaultValue: boolean, options?: EvalOptions): boolean {
        const val = this.getVariationInternal(flagKey, options?.identity || "", options?.context, defaultValue ? "true" : "false");
        return val === "true";
    }

    getNumberValue(flagKey: string, defaultValue: number, options?: EvalOptions): number {
        const val = this.getVariationInternal(flagKey, options?.identity || "", options?.context, String(defaultValue));
        const parsed = Number(val);
        return isNaN(parsed) ? defaultValue : parsed;
    }

    getJsonValue<T>(flagKey: string, defaultValue: T, options?: EvalOptions): T {
        const val = this.getVariationInternal(flagKey, options?.identity || "", options?.context, "");
        if (!val) return defaultValue;
        try {
            return JSON.parse(val) as T;
        } catch {
            return defaultValue;
        }
    }

    track(eventName: string, options?: TrackOptions): void {
        const identity = options?.identity || "";
        if (!identity || !eventName) return;
        this.queueEvent(1, identity, undefined, undefined, eventName, options?.context, options?.value);
    }

    private updateMetrics(flagKey: string, variationId: string) {
        if (this.options.isMetricsEnabled === false) return;

        let m = this.metricsBuffer.get(flagKey);
        if (!m) {
            if (this.metricsBuffer.size >= this.metricsBufferCapacity) return;
            m = new FlagMetrics();
            this.metricsBuffer.set(flagKey, m);
        }
        m.variationsCount[variationId] = (m.variationsCount[variationId] || 0) + 1;
    }

    private queueEvent(type: number, identity: string, flagKey?: string, result?: boolean, eventName?: string, properties?: any, value?: number, variationId?: string) {
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
        if (variationId !== undefined) evt.VariationId = variationId;

        this.eventsBuffer.push(evt);
    }

    private async flushMetrics() {
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
                for (const [vId, count] of Object.entries(item.VariationsCount)) {
                    m.variationsCount[vId] = (m.variationsCount[vId] || 0) + (count as number);
                }
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
