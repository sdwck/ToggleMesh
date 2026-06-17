export interface ToggleMeshOptions {
    baseUrl: string;
    clientKey: string;
    refreshInterval?: number;
}

export interface FlagState {
    key: string;
    isEnabled: boolean;
}

export type ToggleMeshListener = (flags: Record<string, boolean>) => void;

export class ToggleMeshClient {
    private baseUrl: string;
    private clientKey: string;
    private refreshInterval: number;
    private flags: Record<string, boolean> = {};
    private listeners = new Set<ToggleMeshListener>();

    private currentIdentity: string = '';
    private currentContext: Record<string, string> = {};
    private intervalId: any = null;

    constructor(options: ToggleMeshOptions) {
        this.baseUrl = options.baseUrl.replace(/\/$/, '');
        this.clientKey = options.clientKey;
        this.refreshInterval = options.refreshInterval !== undefined ? options.refreshInterval : 60;
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

    
    isEnabled(flagKey: string, defaultValue = false): boolean {
        return this.flags.hasOwnProperty(flagKey) ? this.flags[flagKey] : defaultValue;
    }

    clearIdentity(): void {
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
            this.flags = data.reduce((acc, flag) => {
                acc[flag.key] = flag.isEnabled;
                return acc;
            }, {} as Record<string, boolean>);

            this.notifyListeners();
        } catch (error) {
            console.error('[ToggleMesh] Network error during background sync.', error);
        }
    }

    private notifyListeners(): void {
        this.listeners.forEach(listener => listener(this.flags));
    }
}