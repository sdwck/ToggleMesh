import { useState, useEffect, useRef } from 'react';
import { useExperimentDetails, useExperimentTimeSeries, useStartExperiment, useStopExperiment, useExperimentIterations, useDeleteExperimentIteration, useContextualExperimentDetails, useDeleteContextualRollout, useSetContextualRollout } from '@/api/queries';
import type { ExperimentIterationDto } from '@/api/types';
import { InsightsWidget } from './InsightsWidget';
import { StartExperimentModal } from './StartExperimentModal';
import { RestoreSnapshotModal } from './RestoreSnapshotModal';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Activity, Zap, Beaker, ChevronDown, ChevronUp, Square, Trash2, History, Loader2, Lock, FileText, Download, Undo2, RotateCcw } from 'lucide-react';
import { ToggleMeshIcon } from '@/components/icons/ToggleMeshIcon';
import { formatDistanceToNow, format } from 'date-fns';
import { Button } from '@/components/ui/button';

import { Badge } from '@/components/ui/badge';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend } from 'recharts';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useReactToPrint } from 'react-to-print';
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
    AlertDialogTrigger,
} from "@/components/ui/alert-dialog";


interface Props {
    projectId: string;
    envId: string;
    flagKey: string;
    mabGoalEvent: string | null;
    highlightTrack?: string | null;
    isExperimentActive: boolean;
    isMabEnabled: boolean;
    mabOptimizationType?: number;
    contextPartitionKeys?: string[];
    rolloutPercentage?: number;
    hasRules?: boolean;
    disableScroll?: boolean;
    onStopSuccess?: () => void;
    canEditEnv?: boolean;
    isHistoricalView?: boolean;
    initialHistoricalSnapshot?: any;
}

function MetricCard({ exp, isPrimary }: { exp: any, isPrimary?: boolean }) {
    const prob = Math.round(exp.probabilityToBeatBaseline * 100);
    const uplift = exp.isRevenueBased ? Math.round(exp.expectedValueUplift * 100) : Math.round(exp.expectedUplift * 100);
    const isPositive = uplift > 0;
    const isSignificant = prob >= 95 || prob <= 5;

    return (
        <Card id={`track-${exp.eventName}`} className={`border-border/40 overflow-hidden transition-all duration-1000 print:break-inside-avoid print:shadow-none print:border-zinc-300 ${isPrimary ? 'bg-primary/5 border-primary/20 print:bg-transparent' : 'bg-zinc-950/50 print:bg-transparent'}`}>
            <div className="px-4 py-3 bg-muted/20 border-b border-border/40 print:border-zinc-300 flex items-center justify-between">
                <div className="flex items-center gap-2">
                    <Activity className={`h-4 w-4 ${isPrimary ? 'text-primary print:text-black' : 'text-muted-foreground print:text-zinc-600'}`} />
                    <span className={`font-semibold text-sm ${isPrimary ? 'text-primary print:text-black' : 'print:text-zinc-800'}`}>{exp.eventName || exp.EventName} {isPrimary && '(Optimization Goal)'}</span>
                </div>
                <span className="text-xs text-muted-foreground print:text-zinc-500">
                    Updated {(() => {
                        const raw = exp.lastCalculatedAt ?? exp.LastCalculatedAt;
                        if (!raw) return 'N/A';
                        const d = new Date(raw);
                        return isNaN(d.getTime()) ? 'N/A' : formatDistanceToNow(d, { addSuffix: true });
                    })()}
                </span>
            </div>
            <CardContent className="p-5 space-y-6">
                <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-1 bg-zinc-900/50 print:bg-zinc-50 p-3 rounded border border-border/20 print:border-zinc-200">
                        <div className="text-xs text-muted-foreground print:text-zinc-500 font-medium uppercase tracking-wider mb-2">Control (False)</div>
                        <div className="text-2xl font-bold font-mono print:text-black">
                            {(exp.controlConversionRate * 100).toFixed(1)}<span className="text-sm text-muted-foreground print:text-zinc-500 ml-1">%</span>
                        </div>
                        <div className="text-xs text-muted-foreground/70 print:text-zinc-500">
                            {exp.controlConversions} / {exp.controlExposures} users
                        </div>
                    </div>
                    <div className="space-y-1 bg-zinc-900/50 print:bg-zinc-50 p-3 rounded border border-border/20 print:border-zinc-200">
                        <div className="text-xs text-muted-foreground print:text-zinc-500 font-medium uppercase tracking-wider mb-2">Treatment (True)</div>
                        <div className="text-2xl font-bold font-mono print:text-black">
                            {(exp.treatmentConversionRate * 100).toFixed(1)}<span className="text-sm text-muted-foreground print:text-zinc-500 ml-1">%</span>
                        </div>
                        <div className="text-xs text-muted-foreground/70 print:text-zinc-500">
                            {exp.treatmentConversions} / {exp.treatmentExposures} users
                        </div>
                    </div>
                </div>

                {exp.isRevenueBased && (
                    <div className="grid grid-cols-2 gap-4 mt-4 pt-4 border-t border-border/20 print:border-zinc-200">
                        <div className="space-y-1">
                            <div className="text-[10px] text-muted-foreground print:text-zinc-500 font-medium uppercase tracking-wider">Control Revenue</div>
                            <div className="text-lg font-bold font-mono text-zinc-300 print:text-zinc-800">
                                ${exp.controlTotalValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                            </div>
                            <div className="text-xs text-muted-foreground print:text-zinc-500">
                                ARPU: <span className="font-mono text-zinc-400 print:text-zinc-600">${exp.controlArpu?.toFixed(2) ?? '0.00'}</span>
                            </div>
                        </div>
                        <div className="space-y-1">
                            <div className="text-[10px] text-muted-foreground print:text-zinc-500 font-medium uppercase tracking-wider">Treatment Revenue</div>
                            <div className="text-lg font-bold font-mono text-zinc-300 print:text-zinc-800">
                                ${exp.treatmentTotalValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                            </div>
                            <div className="text-xs text-muted-foreground print:text-zinc-500">
                                ARPU: <span className="font-mono text-emerald-400 print:text-emerald-600">${exp.treatmentArpu?.toFixed(2) ?? '0.00'}</span>
                            </div>
                        </div>
                    </div>
                )}

                <div className="space-y-3">
                    <div className="flex items-center justify-between">
                        <div className="flex items-center gap-1.5">
                            <Zap className={`h-4 w-4 ${isPositive ? 'text-emerald-500' : 'text-rose-500'}`} />
                            <span className="text-sm font-medium print:text-black">{exp.isRevenueBased ? 'Revenue Uplift' : 'Expected Uplift'}</span>
                        </div>
                        <span className={`text-sm font-bold ${isPositive ? 'text-emerald-500' : 'text-rose-500'}`}>
                            {isPositive ? '+' : ''}{uplift}%
                        </span>
                    </div>

                    <div className="space-y-1.5 pt-2">
                        <div className="flex items-center justify-between text-sm">
                            <span className="text-muted-foreground print:text-zinc-500">Probability to Beat Baseline</span>
                            <span className={`font-mono font-bold ${isSignificant ? (prob >= 95 ? 'text-emerald-500' : 'text-rose-500') : 'print:text-zinc-800'}`}>
                                {prob}%
                            </span>
                        </div>
                        <div className="relative h-2 w-full bg-secondary/30 print:bg-zinc-200 rounded-full overflow-hidden print:border print:border-zinc-300">
                            <div
                                className={`absolute top-0 left-0 h-full transition-all ${prob >= 95 ? 'bg-emerald-500 print:bg-emerald-500' : prob <= 5 ? 'bg-rose-500 print:bg-rose-500' : 'bg-primary print:bg-zinc-400'}`}
                                style={{ width: `${prob}%` }}
                            />
                        </div>
                    </div>
                </div>
            </CardContent>
        </Card>
    );
}

function TimeSeriesChart({ projectId, envId, flagKey, eventName }: { projectId: string, envId: string, flagKey: string, eventName: string }) {
    const { data: timeseries, isLoading } = useExperimentTimeSeries(projectId, envId, flagKey, eventName, 1);

    if (isLoading) return <div className="h-16 flex items-center justify-center text-sm text-muted-foreground">Loading time series...</div>;
    if (!timeseries || timeseries.length === 0) return <div className="h-16 flex items-center justify-center text-sm text-muted-foreground">No time series data available yet.</div>;

    const grouped = timeseries.reduce((acc, pt) => {
        if (!acc[pt.time]) acc[pt.time] = { time: pt.time };
        if (pt.variant) {
            acc[pt.time].TreatmentCR = pt.conversionRate * 100;
        } else {
            acc[pt.time].ControlCR = pt.conversionRate * 100;
        }
        return acc;
    }, {} as Record<string, any>);

    const chartData = Object.values(grouped).sort((a: any, b: any) => new Date(a.time).getTime() - new Date(b.time).getTime());

    return (
        <Card className="border-border/40 bg-zinc-950/50 mb-6 print:break-inside-avoid print:shadow-none print:border-zinc-300 print:bg-transparent">
            <CardHeader className="py-4 border-b border-border/40 bg-muted/20 print:border-zinc-300 print:bg-transparent">
                <CardTitle className="text-sm font-semibold flex items-center gap-2 print:text-black">
                    <Activity className="h-4 w-4 text-primary print:text-black" /> Conversion Rate Over Time (24h)
                </CardTitle>
            </CardHeader>
            <CardContent className="p-6">
                <div className="h-[300px] w-full" style={{ minWidth: 0, minHeight: 0 }}>
                    <ResponsiveContainer width="100%" height="100%" minWidth={0}>
                        <LineChart data={chartData} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                            <CartesianGrid strokeDasharray="3 3" stroke="#333" vertical={false} />
                            <XAxis
                                dataKey="time"
                                stroke="#888"
                                fontSize={12}
                                tickFormatter={(val) => format(new Date(val), 'HH:mm')}
                                tickMargin={10}
                            />
                            <YAxis
                                stroke="#888"
                                fontSize={12}
                                tickFormatter={(val) => typeof val === 'number' ? `${val.toFixed(1)}%` : val}
                                width={50}
                            />
                            <Tooltip
                                contentStyle={{ backgroundColor: '#18181b', borderColor: '#333', color: '#fff', borderRadius: '8px' }}
                                itemStyle={{ color: '#fff' }}
                                labelFormatter={(val) => format(new Date(val), 'MMM d, HH:mm')}
                                formatter={(val: any) => typeof val === 'number' ? [`${val.toFixed(2)}%`, undefined] : [val, undefined]}
                            />
                            <Legend verticalAlign="top" height={36} iconType="circle" />
                            <Line type="monotone" name="Control" dataKey="ControlCR" stroke="#888" strokeWidth={3} dot={false} activeDot={{ r: 6 }} />
                            <Line type="monotone" name="Treatment" dataKey="TreatmentCR" stroke="#10b981" strokeWidth={3} dot={false} activeDot={{ r: 6 }} />
                        </LineChart>
                    </ResponsiveContainer>
                </div>
            </CardContent>
        </Card>
    );
}

function SnapshotTimeSeriesChart({ data }: { data: any[] }) {
    if (!data || data.length === 0) return (
        <Card className="border-border/40 bg-zinc-950/50 mb-6 print:break-inside-avoid print:shadow-none print:border-zinc-300 print:bg-transparent">
            <CardHeader className="py-4 border-b border-border/40 bg-muted/20 print:border-zinc-300 print:bg-transparent">
                <CardTitle className="text-sm font-semibold flex items-center gap-2 print:text-black">
                    <Activity className="h-4 w-4 text-primary print:text-black" />
                    Conversion Rate Over Time
                    <Badge variant="outline" className="ml-2 text-[10px] font-normal text-amber-500 border-amber-500/30 print:hidden">Historical</Badge>
                </CardTitle>
            </CardHeader>
            <CardContent className="p-6">
                <div className="h-16 flex items-center justify-center text-sm text-muted-foreground">
                    No time series data available for this historical snapshot.
                </div>
            </CardContent>
        </Card>
    );

    const grouped = data.reduce((acc, pt) => {
        if (!acc[pt.time]) acc[pt.time] = { time: pt.time };
        if (pt.variant) {
            acc[pt.time].TreatmentCR = pt.conversionRate * 100;
        } else {
            acc[pt.time].ControlCR = pt.conversionRate * 100;
        }
        return acc;
    }, {} as Record<string, any>);

    const chartData = Object.values(grouped).sort((a: any, b: any) => new Date(a.time).getTime() - new Date(b.time).getTime());

    if (chartData.length === 0) return null;

    return (
        <Card className="border-border/40 bg-zinc-950/50 mb-6 print:break-inside-avoid print:shadow-none print:border-zinc-300 print:bg-transparent">
            <CardHeader className="py-4 border-b border-border/40 bg-muted/20 print:border-zinc-300 print:bg-transparent">
                <CardTitle className="text-sm font-semibold flex items-center gap-2 print:text-black">
                    <Activity className="h-4 w-4 text-primary print:text-black" />
                    Conversion Rate Over Time
                    <Badge variant="outline" className="ml-2 text-[10px] font-normal text-amber-500 border-amber-500/30 print:hidden">Historical</Badge>
                </CardTitle>
            </CardHeader>
            <CardContent className="p-6">
                <div className="h-[300px] w-full" style={{ minWidth: 0, minHeight: 0 }}>
                    <ResponsiveContainer width="100%" height="100%" minWidth={0}>
                        <LineChart data={chartData} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                            <CartesianGrid strokeDasharray="3 3" stroke="#333" vertical={false} />
                            <XAxis
                                dataKey="time"
                                stroke="#888"
                                fontSize={12}
                                tickFormatter={(val) => format(new Date(val), 'HH:mm')}
                                tickMargin={10}
                            />
                            <YAxis
                                stroke="#888"
                                fontSize={12}
                                tickFormatter={(val) => typeof val === 'number' ? `${val.toFixed(1)}%` : val}
                                width={50}
                            />
                            <Tooltip
                                contentStyle={{ backgroundColor: '#18181b', borderColor: '#333', color: '#fff', borderRadius: '8px' }}
                                itemStyle={{ color: '#fff' }}
                                labelFormatter={(val) => format(new Date(val), 'MMM d, HH:mm')}
                                formatter={(val: any) => typeof val === 'number' ? [`${val.toFixed(2)}%`, undefined] : [val, undefined]}
                            />
                            <Legend verticalAlign="top" height={36} iconType="circle" />
                            <Line type="monotone" name="Control" dataKey="ControlCR" stroke="#888" strokeWidth={3} dot={false} activeDot={{ r: 6 }} />
                            <Line type="monotone" name="Treatment" dataKey="TreatmentCR" stroke="#10b981" strokeWidth={3} dot={false} activeDot={{ r: 6 }} />
                        </LineChart>
                    </ResponsiveContainer>
                </div>
            </CardContent>
        </Card>
    );
}


export function ExperimentResults({ projectId, envId, flagKey, mabGoalEvent, highlightTrack, isExperimentActive, isMabEnabled, mabOptimizationType, contextPartitionKeys, rolloutPercentage, hasRules, disableScroll, onStopSuccess, canEditEnv = true, isHistoricalView = false, initialHistoricalSnapshot = null }: Props) {
    const { data: results, isLoading } = useExperimentDetails(projectId, envId, flagKey);
    const { data: contextualResults, isLoading: isLoadingContextual } = useContextualExperimentDetails(projectId, envId, flagKey);
    const { data: iterations, isLoading: isLoadingIterations } = useExperimentIterations(projectId, envId, flagKey);

    const startMutation = useStartExperiment(projectId, envId, flagKey);
    const stopMutation = useStopExperiment(projectId, envId, flagKey);
    const deleteMutation = useDeleteExperimentIteration(projectId, envId, flagKey);
    const deleteContextualRollout = useDeleteContextualRollout(projectId, envId, flagKey);
    const setContextualRollout = useSetContextualRollout(projectId, envId, flagKey);

    const [showOthers, setShowOthers] = useState(false);
    const [historicalSnapshot, setHistoricalSnapshot] = useState<any | null>(initialHistoricalSnapshot);
    const [restoreSnapshot, setRestoreSnapshot] = useState<ExperimentIterationDto | null>(null);
    const contentRef = useRef<HTMLDivElement>(null);
    const reactToPrintFn = useReactToPrint({ contentRef });
    const hasScrolledRef = useRef(false);

    useEffect(() => {
        setHistoricalSnapshot(initialHistoricalSnapshot);
    }, [initialHistoricalSnapshot]);

    useEffect(() => {
        hasScrolledRef.current = false;
    }, [highlightTrack]);

    useEffect(() => {
        if (!isLoading && results && highlightTrack && !hasScrolledRef.current) {
            hasScrolledRef.current = true;
            if (mabGoalEvent && highlightTrack !== mabGoalEvent) {
                setShowOthers(true);
            }

            if (!disableScroll) {
                setTimeout(() => {
                    const el = document.getElementById(`track-${highlightTrack}`);
                    if (el) {
                        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
                        el.classList.add('ring-2', 'ring-primary', 'ring-offset-2', 'ring-offset-background');
                        setTimeout(() => el.classList.remove('ring-2', 'ring-primary', 'ring-offset-2', 'ring-offset-background'), 2000);
                    }
                }, 100);
            }
        }
    }, [isLoading, results, highlightTrack, mabGoalEvent, disableScroll]);

    let displayResults = results;
    let displayContextual = contextualResults;
    let snapshotTimeSeries: Record<string, any[]> | null = null;
    let displayRolloutPercentage = rolloutPercentage;

    if (historicalSnapshot) {
        try {
            const toCamelCase = (str: string) => str.charAt(0).toLowerCase() + str.slice(1);
            const normalizeKeys = (obj: any): any => {
                if (Array.isArray(obj)) return obj.map(normalizeKeys);
                if (obj && typeof obj === 'object') {
                    return Object.fromEntries(
                        Object.entries(obj).map(([k, v]) => [toCamelCase(k), normalizeKeys(v)])
                    );
                }
                return obj;
            };
            const snapshot = JSON.parse(historicalSnapshot.finalMetricsSnapshot);
            const global = Array.isArray(snapshot?.Global) ? snapshot.Global : Array.isArray(snapshot) ? snapshot : [];
            const contextual = snapshot?.Contextual || [];
            displayResults = normalizeKeys(global);
            displayContextual = normalizeKeys(contextual);

            const rawTs = snapshot?.TimeSeries || snapshot?.timeSeries;
            if (rawTs && typeof rawTs === 'object') {
                snapshotTimeSeries = {};
                for (const [eventName, points] of Object.entries(rawTs)) {
                    snapshotTimeSeries[eventName] = normalizeKeys(points);
                }
            }
            if (historicalSnapshot.flagConfigSnapshot) {
                const config = JSON.parse(historicalSnapshot.flagConfigSnapshot);
                if (config && typeof config.RolloutPercentage === 'number') {
                    displayRolloutPercentage = config.RolloutPercentage;
                } else if (config && typeof config.rolloutPercentage === 'number') {
                    displayRolloutPercentage = config.rolloutPercentage;
                }
            }
        } catch (e) {
            console.error("Failed to parse historical snapshot", e);
        }
    }

    const primaryGoal = mabGoalEvent && displayResults ? displayResults.find((r: any) => (r.eventName || r.EventName) === mabGoalEvent) : null;
    const secondaryMetrics = displayResults ? displayResults.filter((r: any) => (r.eventName || r.EventName) !== mabGoalEvent) : [];

    return (
        <div className="space-y-4 print:bg-white print:p-8" ref={contentRef}>
            <div className="hidden print:flex items-center gap-3 border-b border-zinc-200 pb-6 mb-8">
                <div className="bg-zinc-900 text-white p-2 rounded-lg flex items-center justify-center print:bg-zinc-900 print:text-white print:color-exact print:border-none">
                    <ToggleMeshIcon className="h-6 w-6 print:text-white" />
                </div>
                <div>
                    <h1 className="text-2xl font-bold text-zinc-900 tracking-tight flex items-center gap-2">
                        ToggleMesh
                    </h1>
                    <p className="text-sm text-zinc-500">Experiment Results Report • {flagKey}</p>
                </div>
                <div className="ml-auto text-right">
                    <p className="text-sm font-medium text-zinc-900">{format(new Date(), 'MMMM d, yyyy')}</p>
                    <p className="text-xs text-zinc-500">Generated automatically</p>
                </div>
            </div>

            {historicalSnapshot && (
                <div className="bg-amber-500/10 border border-amber-500/20 rounded-lg p-4 flex items-center justify-between mb-6 print:hidden">
                    <div className="flex items-center gap-3">
                        <History className="h-5 w-5 text-amber-500" />
                        <div>
                            <h4 className="text-amber-500 font-medium text-sm">Viewing Historical Report</h4>
                            <p className="text-xs text-amber-500/80">
                                {format(new Date(historicalSnapshot.startedAt), 'MMM d, yyyy HH:mm')} - {format(new Date(historicalSnapshot.endedAt), 'MMM d, yyyy HH:mm')}
                            </p>
                        </div>
                    </div>
                    <div className="flex items-center gap-2">
                        {!isHistoricalView && (
                            <Button
                                variant="outline"
                                size="sm"
                                className="bg-amber-500/10 border-amber-500/20 text-amber-500 hover:bg-amber-500/20 hover:text-amber-400"
                                onClick={() => { setHistoricalSnapshot(null); setShowOthers(false); }}
                            >
                                Back to Current State
                            </Button>
                        )}
                        <Button
                            variant="default"
                            size="sm"
                            className="bg-emerald-800 hover:bg-emerald-700 text-emerald-50 border border-emerald-700/50"
                            onClick={() => setRestoreSnapshot(historicalSnapshot)}
                            disabled={!canEditEnv}
                        >
                            <Undo2 className="h-4 w-4 mr-2" />
                            Restore Snapshot
                        </Button>
                    </div>
                </div>
            )}

            <div className="bg-zinc-950/50 p-4 rounded-lg border border-border/40 print:hidden flex flex-col gap-4">
                <div className="flex items-start sm:items-center justify-between flex-col sm:flex-row gap-4">
                    <div>
                        <h3 className="font-medium text-sm flex items-center gap-2">
                            Experiment Status
                            {isHistoricalView ? (
                                <span className="flex h-2 w-2 rounded-full bg-zinc-500"></span>
                            ) : isExperimentActive ? (
                                <span className="flex h-2 w-2 rounded-full bg-emerald-500 animate-pulse"></span>
                            ) : (
                                <span className="flex h-2 w-2 rounded-full bg-muted-foreground"></span>
                            )}
                        </h3>
                        <p className="text-xs text-muted-foreground mt-1">
                            {isHistoricalView
                                ? "Historical Iteration. This experiment has been stopped and finalized."
                                : isExperimentActive
                                    ? "Active. The SDK is currently collecting and calculating metrics."
                                    : "Inactive. Exposures and conversions are not being tracked for this flag."}
                        </p>
                    </div>
                    <div className="flex items-center gap-3">
                        <Button
                            variant="outline"
                            size="sm"
                            className="bg-zinc-950/20 border-border/40 hover:bg-muted/20 text-xs h-9"
                            onClick={() => reactToPrintFn()}
                            disabled={!displayResults || displayResults.length === 0}
                        >
                            <Download className="h-4 w-4 mr-2" />
                            Export PDF
                        </Button>
                        {!isHistoricalView && (
                            isExperimentActive ? (
                                <AlertDialog>
                                    <AlertDialogTrigger asChild>
                                        <Button
                                            variant="destructive"
                                            size="sm"
                                            className="bg-rose-500/20 text-rose-500 hover:bg-rose-500/30 border border-rose-500/20 h-9"
                                            disabled={stopMutation.isPending || !canEditEnv}
                                        >
                                            {stopMutation.isPending ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <Square className="h-4 w-4 mr-2" />}
                                            {stopMutation.isPending ? 'Stopping...' : 'Stop & Save Iteration'}
                                        </Button>
                                    </AlertDialogTrigger>
                                    <AlertDialogContent>
                                        <AlertDialogHeader>
                                            <AlertDialogTitle>Stop the active experiment?</AlertDialogTitle>
                                            <AlertDialogDescription>
                                                This will halt data collection for this flag and calculate the final uplift metrics.
                                                The results will be saved as an historical iteration, and the current state will be cleared.
                                            </AlertDialogDescription>
                                        </AlertDialogHeader>
                                        <AlertDialogFooter>
                                            <AlertDialogCancel>Cancel</AlertDialogCancel>
                                            <AlertDialogAction onClick={() => stopMutation.mutate(undefined, { onSuccess: () => onStopSuccess && onStopSuccess() })}>Continue & Stop</AlertDialogAction>
                                        </AlertDialogFooter>
                                    </AlertDialogContent>
                                </AlertDialog>
                            ) : (
                                <StartExperimentModal
                                    projectId={projectId}
                                    envId={envId}
                                    flagKey={flagKey}
                                    currentRolloutPercentage={rolloutPercentage ?? null}
                                    isLoading={startMutation.isPending}
                                    hasRules={hasRules}
                                    onStart={(values) => startMutation.mutate(values)}
                                    canEditEnv={canEditEnv}
                                />
                            )
                        )}
                    </div>
                </div>

                {isExperimentActive && (
                    <div className="pt-4 border-t border-border/20 grid grid-cols-2 md:grid-cols-4 gap-4">
                        <div>
                            <div className="text-[10px] text-muted-foreground uppercase tracking-wider mb-1">Mode</div>
                            <div className={`text-xs font-medium ${isMabEnabled ? 'text-emerald-400' : ''}`}>
                                {isMabEnabled ? 'MAB' : 'A/B Test'}
                            </div>
                        </div>
                        <div>
                            <div className="text-[10px] text-muted-foreground uppercase tracking-wider mb-1">Goal Event</div>
                            <div className="text-xs font-medium">{mabGoalEvent || 'Any'}</div>
                        </div>
                        <div>
                            <div className="text-[10px] text-muted-foreground uppercase tracking-wider mb-1">Optimization</div>
                            <div className="text-xs font-medium">{mabOptimizationType === 1 ? 'Revenue (Value)' : 'Conversion Rate'}</div>
                        </div>
                        <div>
                            <div className="text-[10px] text-muted-foreground uppercase tracking-wider mb-1">Partitions</div>
                            <div className="text-xs font-medium">
                                {contextPartitionKeys && contextPartitionKeys.length > 0
                                    ? contextPartitionKeys.join(', ')
                                    : 'None'}
                            </div>
                        </div>
                    </div>
                )}
            </div>

            <div className="relative">
                {(!isHistoricalView && !isExperimentActive && !historicalSnapshot) && (
                    <div className="absolute inset-0 z-10 bg-background/50 backdrop-blur-[1px] rounded-lg border border-dashed border-border flex items-center justify-center print:hidden">
                        <div className="bg-zinc-950/90 border border-border/40 px-6 py-4 rounded-lg shadow-xl text-center max-w-sm">
                            <Zap className="h-8 w-8 text-muted-foreground/50 mx-auto mb-3" />
                            <h4 className="font-medium text-sm mb-1">Experiment is Paused</h4>
                            <p className="text-xs text-muted-foreground">Start the experiment to begin collecting metrics again. All current progress was saved as a past iteration.</p>
                        </div>
                    </div>
                )}

                <div className={(!isHistoricalView && !isExperimentActive && !historicalSnapshot) ? 'opacity-30 pointer-events-none print:opacity-100 print:pointer-events-auto' : ''}>
                    {isLoading ? (
                        <div className="py-12 text-center text-muted-foreground text-sm">Loading experiment data...</div>
                    ) : !displayResults || displayResults.length === 0 ? (
                        <div className="flex flex-col items-center justify-center py-16 text-center border border-dashed border-border/40 rounded-lg bg-zinc-950/20">
                            <div className="h-12 w-12 bg-muted/30 rounded-full flex items-center justify-center mb-3">
                                <Beaker className="h-6 w-6 text-muted-foreground/60" />
                            </div>
                            <h3 className="text-lg font-medium">No Data Yet</h3>
                            <p className="text-sm text-muted-foreground mt-1 max-w-[250px]">
                                Track events using the ToggleMesh SDK to see A/B testing results.
                            </p>
                        </div>
                    ) : (
                        <div className="space-y-6">
                            {primaryGoal ? (
                                <>
                                    {historicalSnapshot
                                        ? snapshotTimeSeries && snapshotTimeSeries[primaryGoal.eventName] && <SnapshotTimeSeriesChart data={snapshotTimeSeries[primaryGoal.eventName]} />
                                        : <TimeSeriesChart projectId={projectId} envId={envId} flagKey={flagKey} eventName={primaryGoal.eventName} />
                                    }
                                    <MetricCard exp={primaryGoal} isPrimary />

                                    {secondaryMetrics.length > 0 && (
                                        <div className="pt-4 border-t border-border/40 print:border-none print:pt-0">
                                            <Button
                                                variant="ghost"
                                                className="w-full text-muted-foreground hover:text-foreground mb-4 print:hidden"
                                                onClick={() => setShowOthers(!showOthers)}
                                            >
                                                {showOthers ? <ChevronUp className="mr-2 h-4 w-4" /> : <ChevronDown className="mr-2 h-4 w-4" />}
                                                {showOthers ? 'Hide' : 'Show'} {secondaryMetrics.length} Secondary Metrics
                                            </Button>

                                            {showOthers && (
                                                <div className="space-y-6">
                                                    {secondaryMetrics.map((exp, i) => (
                                                        <MetricCard key={i} exp={exp} />
                                                    ))}
                                                </div>
                                            )}
                                        </div>
                                    )}
                                </>
                            ) : (
                                <div className="space-y-6">
                                    {displayResults.map((exp: any, i: number) => (
                                        <MetricCard key={i} exp={exp} />
                                    ))}
                                </div>
                            )}
                        </div>
                    )}
                </div>
            </div>

            {primaryGoal && <InsightsWidget metric={primaryGoal} isActive={historicalSnapshot ? false : isExperimentActive} />}

            {!isLoadingContextual && displayContextual && displayContextual.length > 0 && (
                <div className="pt-8 border-t border-border/40 space-y-4 print:border-none">
                    <h3 className="font-medium text-sm flex items-center gap-2">
                        <Activity className="h-4 w-4 text-primary" />
                        Discovered Contexts
                    </h3>
                    <p className="text-xs text-muted-foreground">
                        {isMabEnabled
                            ? "ToggleMesh is automatically evaluating the MAB goal across these context slices."
                            : "Metrics broken down by discovered contexts. Rollout is global across all contexts."}
                    </p>

                    <Table>
                        <TableHeader>
                            <TableRow className="border-border/40 hover:bg-transparent">
                                <TableHead className="px-2 pl-2">Context Slice</TableHead>
                                <TableHead className="px-2">Goal Event</TableHead>
                                <TableHead className="px-2">Control</TableHead>
                                <TableHead className="px-2">Treatment</TableHead>
                                <TableHead className="px-2">Uplift</TableHead>
                                <TableHead className="px-2">Rollout</TableHead>
                                <TableHead className="w-[40px] pr-2 pl-0 print:hidden"></TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {[...(displayContextual || [])].sort((a, b) => {
                                const sliceA = a?.contextSlice || '';
                                const sliceB = b?.contextSlice || '';
                                if (sliceA === '{}') return -1;
                                if (sliceB === '{}') return 1;
                                return sliceA.localeCompare(sliceB);
                            }).map((iter, i) => {
                                const renderBadges = () => {
                                    if (iter.contextSlice === '{}') return <Badge variant="outline" className="bg-zinc-800 text-zinc-300 border-zinc-700 print:bg-transparent print:border-zinc-300 print:text-zinc-800">Global</Badge>;
                                    try {
                                        const obj = JSON.parse(iter.contextSlice);
                                        return Object.entries(obj).map(([k, v]) => (
                                            <Badge key={k} variant="secondary" className="mr-1 mb-1 font-mono text-[10px] print:bg-zinc-100 print:border print:border-zinc-300 print:text-zinc-800">
                                                <span className="text-muted-foreground mr-1 print:text-zinc-500">{k}:</span>{String(v)}
                                            </Badge>
                                        ));
                                    } catch {
                                        return <span className="font-mono print:text-zinc-800">{iter.contextSlice}</span>;
                                    }
                                };
                                return (
                                    <TableRow key={i} className="border-border/40">
                                        <TableCell className="text-xs px-2 pl-2">
                                            <div className="flex flex-wrap max-w-[200px]">
                                                {renderBadges()}
                                            </div>
                                        </TableCell>
                                        <TableCell className="text-xs text-muted-foreground px-2">
                                            {iter.eventName}
                                        </TableCell>
                                        <TableCell className="text-xs text-muted-foreground px-2">
                                            {(iter.controlConversionRate * 100).toFixed(1)}% ({iter.controlExposures})
                                        </TableCell>
                                        <TableCell className="text-xs text-muted-foreground px-2">
                                            {(iter.treatmentConversionRate * 100).toFixed(1)}% ({iter.treatmentExposures})
                                        </TableCell>
                                        <TableCell className="px-2">
                                            <span className={`font-bold text-xs ${iter.expectedUplift > 0 ? 'text-emerald-500' : 'text-rose-500'}`}>
                                                {iter.expectedUplift > 0 ? '+' : ''}{(iter.expectedUplift * 100).toFixed(1)}%
                                            </span>
                                        </TableCell>
                                        <TableCell className="px-2">
                                            <div className="flex items-center gap-1 group">
                                                <input
                                                    key={iter.contextSlice === '{}' ? (displayRolloutPercentage ?? 50) : (iter.currentRollout ?? 'global')}
                                                    type="number"
                                                    defaultValue={iter.contextSlice === '{}' ? (displayRolloutPercentage ?? 50) : (iter.currentRollout ?? '')}
                                                    placeholder={iter.contextSlice === '{}' ? '50' : 'Global'}
                                                    disabled={!canEditEnv || setContextualRollout.isPending || !!historicalSnapshot}
                                                    className={`peer w-[6ch] bg-transparent outline-none border-b border-dashed border-primary/40 hover:border-primary/80 focus:border-primary transition-colors text-center appearance-none [&::-webkit-inner-spin-button]:appearance-none font-mono text-xs font-semibold ${!canEditEnv || setContextualRollout.isPending || !!historicalSnapshot ? 'cursor-not-allowed opacity-50' : 'cursor-text text-primary/80 focus:text-primary'}`}
                                                    onBlur={(e) => {
                                                        if (!!historicalSnapshot) return;
                                                        if (!e.target.value && iter.contextSlice !== '{}' && iter.currentRollout == null) return;
                                                        let val = parseInt(e.target.value || '0', 10);
                                                        if (isNaN(val)) val = 0;
                                                        val = Math.max(0, Math.min(100, val));
                                                        
                                                        const oldVal = iter.contextSlice === '{}' ? (displayRolloutPercentage ?? 50) : iter.currentRollout;
                                                        if (val !== oldVal) {
                                                            setContextualRollout.mutate({ contextSlice: iter.contextSlice, rolloutPercentage: val });
                                                        } else {
                                                            e.target.value = iter.contextSlice === '{}' ? val.toString() : (iter.currentRollout?.toString() ?? '');
                                                        }
                                                    }}
                                                    onKeyDown={(e) => {
                                                        if (!!historicalSnapshot) {
                                                            e.preventDefault();
                                                            return;
                                                        }
                                                        if (e.key === 'Enter') {
                                                            e.preventDefault();
                                                            e.currentTarget.blur();
                                                        } else if (e.key === 'Escape') {
                                                            e.preventDefault();
                                                            e.currentTarget.value = iter.contextSlice === '{}' ? (displayRolloutPercentage ?? 50).toString() : (iter.currentRollout?.toString() ?? '');
                                                            e.currentTarget.blur();
                                                        }
                                                    }}
                                                    title={canEditEnv && !historicalSnapshot ? "Click to edit rollout" : undefined}
                                                />
                                                <span className={`text-muted-foreground text-xs ${iter.contextSlice !== '{}' && iter.currentRollout == null ? 'opacity-0 peer-focus:opacity-100 transition-opacity' : ''}`}>%</span>
                                                {iter.isAutoManaged === false && (
                                                    <div title="Pinned by user" className="ml-1">
                                                        <Lock className="h-3 w-3 text-amber-500/80" />
                                                    </div>
                                                )}
                                            </div>
                                        </TableCell>
                                        <TableCell className="pr-2 pl-0 print:hidden">
                                            {iter.isAutoManaged === false && (
                                                <div className="flex items-center gap-1">
                                                    <AlertDialog>
                                                        <AlertDialogTrigger asChild>
                                                            <Button
                                                                variant="ghost"
                                                                size="icon"
                                                                className="h-7 w-7 text-muted-foreground hover:text-blue-500 hover:bg-blue-500/10"
                                                                disabled={deleteContextualRollout.isPending || !canEditEnv || !!historicalSnapshot}
                                                                title="Reset to auto managed"
                                                            >
                                                                <RotateCcw className="h-3.5 w-3.5" />
                                                            </Button>
                                                        </AlertDialogTrigger>
                                                        <AlertDialogContent>
                                                            <AlertDialogHeader>
                                                                <AlertDialogTitle>Reset to auto-managed?</AlertDialogTitle>
                                                                <AlertDialogDescription>
                                                                    This will remove your manual override for this context slice. The MAB algorithm will automatically recalculate the optimal rollout percentage on the next cycle.
                                                                </AlertDialogDescription>
                                                            </AlertDialogHeader>
                                                            <AlertDialogFooter>
                                                                <AlertDialogCancel>Cancel</AlertDialogCancel>
                                                                <AlertDialogAction
                                                                    onClick={() => deleteContextualRollout.mutate(iter.contextSlice)}
                                                                >
                                                                    Reset to Auto
                                                                </AlertDialogAction>
                                                            </AlertDialogFooter>
                                                        </AlertDialogContent>
                                                    </AlertDialog>
                                                </div>
                                            )}
                                        </TableCell>
                                    </TableRow>
                                );
                            })}
                        </TableBody>
                    </Table>
                </div>
            )}

            {!isHistoricalView && !isLoadingIterations && iterations && iterations.length > 0 && (
                <div className="pt-8 border-t border-border/40 space-y-4 print:hidden">
                    <h3 className="font-medium text-sm flex items-center gap-2">
                        <History className="h-4 w-4 text-muted-foreground" />
                        Experiment History
                    </h3>

                    <div className="max-h-[400px] overflow-auto rounded-md border border-border/40 print:max-h-none print:border-none print:overflow-visible">
                        <Table>
                            <TableHeader className="sticky top-0 bg-zinc-950 z-10">
                                <TableRow className="border-border/40 hover:bg-transparent">
                                    <TableHead className="w-[180px]">Started</TableHead>
                                    <TableHead className="w-[180px]">Ended</TableHead>
                                    <TableHead>Results Snapshot</TableHead>
                                    <TableHead className="w-[60px] print:hidden"></TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {iterations.map(iter => {
                                    const snapshot = JSON.parse(iter.finalMetricsSnapshot);
                                    const configSnapshot = iter.flagConfigSnapshot ? JSON.parse(iter.flagConfigSnapshot) : {};
                                    const iterationGoalEvent = configSnapshot.MabGoalEvent || configSnapshot.mabGoalEvent;

                                    const metrics = Array.isArray(snapshot?.Global) ? snapshot.Global : Array.isArray(snapshot) ? snapshot : [];

                                    let primary;
                                    if (Array.isArray(metrics) && metrics.length > 0) {
                                        if (iterationGoalEvent) {
                                            primary = metrics.find((m: any) => (m.EventName || m.eventName) === iterationGoalEvent);
                                        }
                                        if (!primary) {
                                            primary = [...metrics].sort((a: any, b: any) => (b.ControlExposures || 0) - (a.ControlExposures || 0))[0];
                                        }
                                    }

                                    return (
                                        <TableRow key={iter.id} className="border-border/40">
                                            <TableCell className="text-xs text-muted-foreground">
                                                {format(new Date(iter.startedAt), 'MMM d, yyyy HH:mm')}
                                            </TableCell>
                                            <TableCell className="text-xs text-muted-foreground">
                                                {format(new Date(iter.endedAt), 'MMM d, yyyy HH:mm')}
                                            </TableCell>
                                            <TableCell>
                                                {primary ? (
                                                    <div className="flex items-center gap-4 text-xs">
                                                        <span className="font-medium text-foreground">{primary.EventName}</span>
                                                        <span className="text-muted-foreground">
                                                            Control: {(primary.ControlConversionRate * 100).toFixed(1)}% ({primary.ControlExposures})
                                                        </span>
                                                        <span className="text-muted-foreground">
                                                            Treatment: {(primary.TreatmentConversionRate * 100).toFixed(1)}% ({primary.TreatmentExposures})
                                                        </span>
                                                        <span className={`font-bold ${primary.ExpectedUplift > 0 ? 'text-emerald-500' : 'text-rose-500'}`}>
                                                            {primary.ExpectedUplift > 0 ? '+' : ''}{(primary.ExpectedUplift * 100).toFixed(1)}% uplift
                                                        </span>
                                                    </div>
                                                ) : (
                                                    <span className="text-xs text-muted-foreground">No metric data</span>
                                                )}
                                            </TableCell>
                                            <TableCell className='print:hidden'>
                                                <div className="flex items-center gap-2">
                                                    <Button
                                                        variant="ghost"
                                                        size="icon"
                                                        className="h-8 w-8 text-muted-foreground hover:text-primary"
                                                        title="View Historical Snapshot"
                                                        onClick={() => {
                                                            setHistoricalSnapshot(iter);
                                                            window.scrollTo({ top: 0, behavior: 'smooth' });
                                                        }}
                                                    >
                                                        <FileText className="h-4 w-4" />
                                                    </Button>

                                                    <AlertDialog>
                                                        <AlertDialogTrigger asChild>
                                                            <Button
                                                                variant="ghost"
                                                                size="icon"
                                                                className="h-8 w-8 text-muted-foreground hover:text-rose-500 hover:bg-rose-500/10"
                                                                disabled={deleteMutation.isPending || !canEditEnv}
                                                            >
                                                                {deleteMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
                                                            </Button>
                                                        </AlertDialogTrigger>
                                                        <AlertDialogContent>
                                                            <AlertDialogHeader>
                                                                <AlertDialogTitle>Delete this iteration?</AlertDialogTitle>
                                                                <AlertDialogDescription>
                                                                    Are you sure you want to delete this historical experiment data? This action cannot be undone.
                                                                </AlertDialogDescription>
                                                            </AlertDialogHeader>
                                                            <AlertDialogFooter>
                                                                <AlertDialogCancel>Cancel</AlertDialogCancel>
                                                                <AlertDialogAction
                                                                    className="bg-rose-500 hover:bg-rose-600 text-white"
                                                                    onClick={() => deleteMutation.mutate(iter.id)}
                                                                >
                                                                    Delete Iteration
                                                                </AlertDialogAction>
                                                            </AlertDialogFooter>
                                                        </AlertDialogContent>
                                                    </AlertDialog>
                                                </div>
                                            </TableCell>
                                        </TableRow>
                                    );
                                })}
                            </TableBody>
                        </Table>
                    </div>
                </div>
            )}
            {restoreSnapshot && (
                <RestoreSnapshotModal
                    open={!!restoreSnapshot}
                    onOpenChange={(open) => !open && setRestoreSnapshot(null)}
                    projectId={projectId}
                    envId={envId}
                    flagKey={flagKey}
                    iteration={restoreSnapshot}
                />
            )}
        </div>
    );
}
