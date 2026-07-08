import { useState, useEffect, useRef, useMemo } from 'react';
import { useParams } from 'react-router-dom';
import { Terminal, Play, Pause, Trash2, Filter } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useProjectDetails } from '@/api/queries';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Input } from '@/components/ui/input';
import { useRealTimeStore } from '@/stores/useRealTimeStore';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import {
    Form,
    FormControl,
    FormField,
    FormItem,
} from '@/components/ui/form';

const terminalFilterSchema = z.object({
    environmentId: z.string().optional(),
    filterKey: z.string().optional()
});
type TerminalFilterValues = z.infer<typeof terminalFilterSchema>;

interface LogEntry {
    id: string;
    timestamp: Date;
    type: 'exposure' | 'track';
    flagKey?: string;
    eventName?: string;
    identity: string;
    result?: boolean;
    properties?: any;
    value?: number;
}

export function TerminalPage() {
    const { projectId } = useParams();
    const { data: project } = useProjectDetails(projectId || '');
    const subscribe = useRealTimeStore(s => s.subscribe);
    const unsubscribe = useRealTimeStore(s => s.unsubscribe);

    const form = useForm<TerminalFilterValues>({
        resolver: zodResolver(terminalFilterSchema),
        defaultValues: {
            environmentId: '',
            filterKey: ''
        }
    });

    const [isPaused, setIsPaused] = useState(false);
    const [logs, setLogs] = useState<LogEntry[]>([]);
    const [showEval, setShowEval] = useState(true);
    const [showTrack, setShowTrack] = useState(true);

    const logsRef = useRef<LogEntry[]>([]);
    const isPausedRef = useRef(false);

    const selectedEnvId = form.watch('environmentId');
    const filterKey = form.watch('filterKey');

    useEffect(() => {
        isPausedRef.current = isPaused;
    }, [isPaused]);

    useEffect(() => {
        if (project?.environments?.length && !selectedEnvId) {
            form.setValue('environmentId', project.environments[0].id);
        }
    }, [project, selectedEnvId, form]);

    useEffect(() => {
        if (!selectedEnvId) return;

        const topic = `livetail:${selectedEnvId}`;

        const handleEvent = (data: any) => {
            if (isPausedRef.current) return;

            const entry: LogEntry = {
                id: Math.random().toString(36).substring(7),
                timestamp: new Date(),
                type: data.EventName ? 'track' : 'exposure',
                flagKey: data.FlagKey,
                eventName: data.EventName,
                identity: data.Identity,
                result: data.Result,
                properties: data.Properties,
                value: data.Value
            };

            logsRef.current = [entry, ...logsRef.current].slice(0, 500);
            setLogs(logsRef.current);
        };

        subscribe(topic, handleEvent);
        return () => {
            unsubscribe(topic, handleEvent);
        };
    }, [selectedEnvId, subscribe, unsubscribe]);

    const handleClear = () => {
        logsRef.current = [];
        setLogs([]);
    };

    const filteredLogs = useMemo(() => {
        return logs.filter(l => {
            if (!showEval && l.type === 'exposure') return false;
            if (!showTrack && l.type === 'track') return false;

            if (!filterKey) return true;

            const search = filterKey.toLowerCase();

            const inFlag = l.flagKey?.toLowerCase().includes(search);
            const inEvent = l.eventName?.toLowerCase().includes(search);
            const inIdentity = l.identity?.toLowerCase().includes(search);
            const inValue = l.value?.toString().includes(search);
            const inProps = l.properties ? JSON.stringify(l.properties).toLowerCase().includes(search) : false;

            return inFlag || inEvent || inIdentity || inValue || inProps;
        });
    }, [logs, filterKey, showEval, showTrack]);

    return (
        <div className="flex flex-col h-full bg-zinc-950 border border-border/40 rounded-xl overflow-hidden shadow-2xl">
            <div className="flex flex-col xl:flex-row items-start xl:items-center justify-between p-3 border-b border-border/40 bg-zinc-900/50 gap-4">
                <div className="flex flex-col lg:flex-row items-start lg:items-center gap-4 w-full xl:w-auto">
                    <div className="flex items-center justify-between w-full lg:w-auto">
                        <div className="flex items-center gap-2 text-zinc-400">
                            <Terminal className="w-4 h-4 text-primary shrink-0" />
                            <span className="text-sm font-semibold tracking-wider uppercase shrink-0">Live Tail</span>
                        </div>
                        <div className="lg:hidden flex items-center gap-2 shrink-0">
                            <Button
                                variant={isPaused ? "default" : "secondary"}
                                size="sm"
                                className="h-8 px-2 sm:px-3 text-xs gap-1.5"
                                onClick={() => setIsPaused(!isPaused)}
                            >
                                {isPaused ? <Play className="w-3.5 h-3.5" /> : <Pause className="w-3.5 h-3.5" />}
                                <span className="hidden sm:inline">{isPaused ? 'Resume' : 'Pause'}</span>
                            </Button>
                            <Button
                                variant="ghost"
                                size="sm"
                                className="h-8 px-2 sm:px-3 text-xs gap-1.5 text-zinc-400 hover:text-red-400 hover:bg-red-500/10"
                                onClick={handleClear}
                            >
                                <Trash2 className="w-3.5 h-3.5" />
                                <span className="hidden sm:inline">Clear</span>
                            </Button>
                        </div>
                    </div>

                    <div className="hidden lg:block h-4 w-px bg-border/50 mx-2 shrink-0" />

                    <Form {...form}>
                        <form className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3 w-full" onSubmit={(e) => e.preventDefault()}>
                            <FormField
                                control={form.control}
                                name="environmentId"
                                render={({ field }) => (
                                    <FormItem className="space-y-0 w-full sm:w-[180px] shrink-0">
                                        <FormControl>
                                            <Select value={field.value} onValueChange={field.onChange}>
                                                <SelectTrigger className="w-full h-8 text-xs bg-zinc-950 border-zinc-800">
                                                    <SelectValue placeholder="Select Environment" />
                                                </SelectTrigger>
                                                <SelectContent>
                                                    {project?.environments.map(env => (
                                                        <SelectItem key={env.id} value={env.id} className="text-xs">
                                                            {env.name}
                                                        </SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </FormControl>
                                    </FormItem>
                                )}
                            />

                            <div className="flex items-center bg-zinc-950 border border-zinc-800 rounded-md p-0.5 h-8 w-full sm:w-auto shrink-0">
                                <Button
                                    type="button"
                                    variant="ghost"
                                    size="sm"
                                    onClick={() => setShowEval(!showEval)}
                                    className={`flex-1 sm:flex-none h-6 px-2 text-[11px] font-mono rounded-sm rounded-r-none transition-all ${showEval ? 'bg-blue-500/20 text-blue-300' : 'text-zinc-600 hover:text-zinc-400'
                                        }`}
                                >
                                    EVAL
                                </Button>
                                <Button
                                    type="button"
                                    variant="ghost"
                                    size="sm"
                                    onClick={() => setShowTrack(!showTrack)}
                                    className={`flex-1 sm:flex-none h-6 px-2 text-[11px] font-mono rounded-sm rounded-l-none transition-all ${showTrack ? 'bg-purple-500/20 text-purple-300' : 'text-zinc-600 hover:text-zinc-400'
                                        }`}
                                >
                                    TRACK
                                </Button>
                            </div>

                            <FormField
                                control={form.control}
                                name="filterKey"
                                render={({ field }) => (
                                    <FormItem className="space-y-0 w-full sm:w-[200px]">
                                        <FormControl>
                                            <div className="relative">
                                                <Filter className="absolute left-2.5 top-2 h-3.5 w-3.5 text-zinc-500" />
                                                <Input
                                                    placeholder="Filter by flag or event..."
                                                    className="h-8 w-full pl-8 text-xs bg-zinc-950 border-zinc-800 focus-visible:ring-primary/50"
                                                    {...field}
                                                    value={field.value || ''}
                                                />
                                            </div>
                                        </FormControl>
                                    </FormItem>
                                )}
                            />
                        </form>
                    </Form>
                </div>

                <div className="hidden lg:flex items-center gap-2 shrink-0">
                    <Button
                        variant={isPaused ? "default" : "secondary"}
                        size="sm"
                        className="h-8 px-3 text-xs gap-1.5"
                        onClick={() => setIsPaused(!isPaused)}
                    >
                        {isPaused ? <Play className="w-3.5 h-3.5" /> : <Pause className="w-3.5 h-3.5" />}
                        {isPaused ? 'Resume' : 'Pause'}
                    </Button>
                    <Button
                        variant="ghost"
                        size="sm"
                        className="h-8 px-3 text-xs gap-1.5 text-zinc-400 hover:text-red-400 hover:bg-red-500/10"
                        onClick={handleClear}
                    >
                        <Trash2 className="w-3.5 h-3.5" />
                        Clear
                    </Button>
                </div>
            </div>

            <div className="flex-1 overflow-y-auto bg-[#0c0c0e] font-mono text-[13px] leading-relaxed p-4">
                {filteredLogs.length === 0 ? (
                    <div className="flex flex-col items-center justify-center h-full text-zinc-600 space-y-3">
                        <Terminal className="w-8 h-8 opacity-20" />
                        <p>Waiting for events in selected environment...</p>
                    </div>
                ) : (
                    <div className="space-y-0.5">
                        {filteredLogs.map(log => (
                            <div key={log.id} className="grid grid-cols-[110px_60px_1fr] gap-3 hover:bg-zinc-800/50 px-2 py-1.5 rounded transition-colors items-start">
                                <span className="text-zinc-500 shrink-0 select-none text-[12px] pt-0.5">
                                    {`${log.timestamp.getHours().toString().padStart(2, '0')}:${log.timestamp.getMinutes().toString().padStart(2, '0')}:${log.timestamp.getSeconds().toString().padStart(2, '0')}.${log.timestamp.getMilliseconds().toString().padStart(3, '0')}`}
                                </span>
                                <span className="pt-0.5">
                                    {log.type === 'exposure' ? (
                                        <span className="text-blue-400/80 font-semibold text-[11px] uppercase tracking-wider">EVAL</span>
                                    ) : (
                                        <span className="text-purple-400/80 font-semibold text-[11px] uppercase tracking-wider">TRACK</span>
                                    )}
                                </span>
                                <div className="flex flex-col gap-1 overflow-hidden">
                                    {log.type === 'exposure' ? (
                                        <div className="flex items-center gap-2 flex-wrap">
                                            <span className="text-zinc-200 font-medium">{log.flagKey}</span>
                                            <span className="text-zinc-600">for</span>
                                            <span className="text-zinc-400">{log.identity || 'anonymous'}</span>
                                            <span className="text-zinc-600">→</span>
                                            <span className={log.result ? "text-emerald-400 font-medium" : "text-rose-400 font-medium"}>
                                                {log.result ? 'TRUE' : 'FALSE'}
                                            </span>
                                        </div>
                                    ) : (
                                        <div className="flex items-center gap-2 flex-wrap">
                                            <span className="text-zinc-200 font-medium">{log.eventName}</span>
                                            <span className="text-zinc-600">for</span>
                                            <span className="text-zinc-400">{log.identity || 'anonymous'}</span>
                                            {log.value !== undefined && log.value !== null && (
                                                <>
                                                    <span className="text-zinc-600 ml-1">value:</span>
                                                    <span className="text-amber-400/90 font-mono">{log.value}</span>
                                                </>
                                            )}
                                        </div>
                                    )}
                                    {log.properties && Object.keys(log.properties).length > 0 && (
                                        <div className="text-zinc-500 text-[11px] truncate">
                                            {JSON.stringify(log.properties)}
                                        </div>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}
