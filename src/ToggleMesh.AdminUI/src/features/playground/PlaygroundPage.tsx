import { useState, useEffect, useRef } from 'react';
import { ToggleMeshClient } from 'togglemesh-js';
import { ToggleMeshProvider, useFeatureFlag } from 'togglemesh-js/react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ToggleRight, User, ShieldCheck, Terminal, HelpCircle } from 'lucide-react';
import { toast } from 'sonner';

export function PlaygroundPage() {
    const [baseUrl, setBaseUrl] = useState(window.location.origin);
    const [clientKey, setClientKey] = useState('');
    const [identity, setIdentity] = useState('user_123');
    const [contextJson, setContextJson] = useState('{\n  "Country": "US",\n  "Plan": "Pro"\n}');

    const [client, setClient] = useState<ToggleMeshClient | null>(null);
    const [logs, setLogs] = useState<string[]>([]);
    const logsEndRef = useRef<HTMLDivElement>(null);

    const addLog = (msg: string, type: 'info' | 'success' | 'warn' | 'error' = 'info') => {
        const time = new Date().toLocaleTimeString();
        const prefix = type === 'success' ? '✔' : type === 'warn' ? '⚠' : type === 'error' ? '✘' : 'ℹ';
        setLogs(prev => [...prev, `[${time}] ${prefix} ${msg}`]);
    };

    useEffect(() => {
        if (logsEndRef.current) {
            logsEndRef.current.scrollIntoView({ behavior: 'smooth' });
        }
    }, [logs]);

    const handleConnect = async () => {
        if (!clientKey.trim()) {
            toast.error('API Key is required');
            return;
        }

        try {
            let parsedContext = {};
            try {
                parsedContext = JSON.parse(contextJson);
            } catch {
                toast.error('Invalid Context JSON');
                return;
            }
            
            const tmClient = new ToggleMeshClient({
                baseUrl,
                clientKey: clientKey.trim(),
                refreshInterval: 5
            });

            addLog(`Initializing ToggleMeshClient with API Key: ${clientKey.slice(0, 15)}...`, 'info');
            addLog(`Identifying session: '${identity}' with context: ${JSON.stringify(parsedContext)}`, 'info');

            await tmClient.identify(identity, parsedContext);

            setClient(tmClient);
            addLog('SDK successfully connected to Data Plane! Real-time polling active (5s).', 'success');
            toast.success('Connected to ToggleMesh SDK');
        } catch (err: any) {
            addLog(`Connection failed: ${err.message}`, 'error');
            toast.error('Failed to initialize SDK');
        }
    };

    return (
        <div className="space-y-6">
            <div>
                <h2 className="text-2xl font-bold tracking-tight">SDK Playground</h2>
                <p className="text-muted-foreground">Test your Client-Side JS SDK evaluations and real-time updates in live environment.</p>
            </div>

            <div className="grid gap-6 md:grid-cols-3">
                <Card className="border-border/40 bg-zinc-950/20 md:col-span-1 h-fit">
                    <CardHeader>
                        <CardTitle className="text-sm font-semibold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
                            <ShieldCheck className="h-4 w-4" /> Connection Settings
                        </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div className="space-y-1.5">
                            <Label>Base API URL</Label>
                            <Input value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} className="bg-zinc-950/40 text-xs font-mono" />
                        </div>
                        <div className="space-y-1.5">
                            <Label>API Key (Server or Client)</Label>
                            <Input
                                type="password"
                                placeholder="Paste your API key"
                                value={clientKey}
                                onChange={(e) => setClientKey(e.target.value)}
                                className="bg-zinc-950/40 text-xs font-mono"
                            />
                        </div>
                        <div className="space-y-1.5">
                            <Label>User Identity (Unique ID)</Label>
                            <Input value={identity} onChange={(e) => setIdentity(e.target.value)} className="bg-zinc-950/40 text-xs font-mono" />
                        </div>
                        <div className="space-y-1.5">
                            <Label>Context Attributes (JSON)</Label>
                            <textarea
                                value={contextJson}
                                onChange={(e) => setContextJson(e.target.value)}
                                rows={4}
                                className="w-full bg-zinc-950/40 border border-border/40 rounded-md p-2 font-mono text-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                            />
                        </div>
                        <Button className="w-full cursor-pointer" onClick={handleConnect}>
                            Connect & Evaluate
                        </Button>
                    </CardContent>
                </Card>

                <div className="md:col-span-2 space-y-6 flex flex-col">
                    {client ? (
                        <ToggleMeshProvider client={client}>
                            <PlaygroundSandbox client={client} logs={logs} addLog={addLog} logsEndRef={logsEndRef} />
                        </ToggleMeshProvider>
                    ) : (
                        <Card className="border-border/40 bg-zinc-950/20 flex-1 flex flex-col items-center justify-center p-8 text-center border-dashed">
                            <HelpCircle className="h-10 w-10 text-muted-foreground mb-4 animate-bounce" />
                            <h3 className="font-semibold text-lg">Waiting for connection</h3>
                            <p className="text-muted-foreground text-sm max-w-sm mt-1">
                                Configure your API Key and click "Connect" to spin up the live React evaluation sandbox.
                            </p>
                        </Card>
                    )}
                </div>
            </div>
        </div>
    );
}

function PlaygroundSandbox({ client, logs, addLog, logsEndRef }: { client: ToggleMeshClient, logs: string[], addLog: (msg: string, type?: any) => void, logsEndRef: any }) {
    const [customFlag, setCustomFlag] = useState('new-checkout-flow');
    
    const isEnabled = useFeatureFlag(customFlag, false);

    useEffect(() => {
        return client.subscribe((flags) => {
            addLog(`Local cache updated in memory. Current pool: ${JSON.stringify(flags)}`, 'success');
        });
    }, [client]);

    return (
        <div className="space-y-6 flex-1 flex flex-col">
            <Card className="border-border/40 bg-zinc-950/20">
                <CardHeader>
                    <CardTitle className="text-sm font-semibold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
                        <ToggleRight className="h-4 w-4 text-primary" /> Live Evaluation Sandbox
                    </CardTitle>
                </CardHeader>
                <CardContent className="space-y-6">
                    <div className="flex items-end gap-3 max-w-sm">
                        <div className="space-y-1.5 flex-1">
                            <Label>Verify custom Flag Key</Label>
                            <Input value={customFlag} onChange={(e) => setCustomFlag(e.target.value)} className="bg-zinc-950/40 text-xs font-mono h-9" />
                        </div>
                    </div>

                    <div className="flex items-center gap-6 p-6 border border-border/40 rounded-lg bg-zinc-950/40 w-fit min-w-[280px]">
                        <div className={`h-12 w-12 rounded-full border-2 flex items-center justify-center transition-all duration-300 ${
                            isEnabled
                                ? 'bg-emerald-500/10 border-emerald-500 shadow-[0_0_15px_rgba(16,185,129,0.2)]'
                                : 'bg-zinc-900/60 border-zinc-800'
                        }`}>
                            <User className={`h-6 w-6 transition-colors ${isEnabled ? 'text-emerald-400' : 'text-zinc-600'}`} />
                        </div>
                        <div>
                            <div className="text-[10px] text-muted-foreground uppercase font-mono tracking-wider">Evaluation result</div>
                            <div className={`text-lg font-bold font-mono tracking-wide ${isEnabled ? 'text-emerald-400' : 'text-zinc-500'}`}>
                                {isEnabled ? 'ENABLED (TRUE)' : 'DISABLED (FALSE)'}
                            </div>
                        </div>
                    </div>
                </CardContent>
            </Card>

            <Card className="border-border/40 bg-zinc-950/20 flex-1 flex flex-col">
                <CardHeader className="pb-2">
                    <CardTitle className="text-sm font-semibold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
                        <Terminal className="h-4 w-4" /> Live SDK Logs
                    </CardTitle>
                </CardHeader>
                <CardContent className="flex-1 flex flex-col pr-1">
                    <div className="flex-1 bg-black/40 border border-border/20 rounded-lg px-4 py-2 font-mono text-[10px] overflow-y-auto space-y-2 max-h-[7.75rem] scrollbar-thin">
                        {logs.map((log, index) => {
                            let color = 'text-zinc-400';
                            if (log.includes('✔')) color = 'text-emerald-400';
                            if (log.includes('⚠')) color = 'text-amber-500';
                            if (log.includes('✘')) color = 'text-rose-500';
                            return (
                                <div key={index} className={color}>
                                    {log}
                                </div>
                            );
                        })}
                        <div ref={logsEndRef} />
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}