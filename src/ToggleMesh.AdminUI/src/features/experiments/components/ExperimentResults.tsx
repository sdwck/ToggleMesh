import { useState, useEffect, useRef } from 'react';
import { useExperimentDetails, useStartExperiment, useStopExperiment, useExperimentIterations, useDeleteExperimentIteration, useContextualExperimentDetails, useDeleteContextualRollout, useSetContextualRollout } from '@/api/queries';
import type { ExperimentIterationDto } from '@/api/types';
import { InsightsWidget } from './InsightsWidget';
import { StartExperimentModal } from './StartExperimentModal';
import { RestoreSnapshotModal } from './RestoreSnapshotModal';
import { MetricCard } from './MetricCard';
import { TimeSeriesChart, SnapshotTimeSeriesChart } from './TimeSeriesChart';
import { ContextualRolloutManager } from './ContextualRolloutManager';
import { ExperimentHistoryTable } from './ExperimentHistoryTable';
import { Zap, Beaker, ChevronDown, ChevronUp, Square, History, Loader2, Download, Undo2 } from 'lucide-react';
import { ToggleMeshIcon } from '@/components/icons/ToggleMeshIcon';
import { format } from 'date-fns';
import { Button } from '@/components/ui/button';
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


export function ExperimentResults({ projectId, envId, flagKey, mabGoalEvent, highlightTrack, isExperimentActive, isMabEnabled, mabOptimizationType, contextPartitionKeys, rolloutPercentage, hasRules, disableScroll, onStopSuccess, canEditEnv = true, isHistoricalView = false, initialHistoricalSnapshot = null }: Props) {
    const { data: results, isLoading } = useExperimentDetails(projectId, envId, flagKey);
    const { data: contextualResults } = useContextualExperimentDetails(projectId, envId, flagKey);
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
                <div className="bg-amber-500/10 border border-amber-500/20 rounded-lg p-4 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 mb-6 print:hidden">
                    <div className="flex items-center gap-3">
                        <History className="h-5 w-5 text-amber-500 shrink-0" />
                        <div>
                            <h4 className="text-amber-500 font-medium text-sm">Viewing Historical Report</h4>
                            <p className="text-xs text-amber-500/80">
                                {format(new Date(historicalSnapshot.startedAt), 'MMM d, yyyy HH:mm')} - {format(new Date(historicalSnapshot.endedAt), 'MMM d, yyyy HH:mm')}
                            </p>
                        </div>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
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
                    <div className="flex flex-wrap items-center gap-2 sm:gap-3 mt-3 sm:mt-0 w-full sm:w-auto">
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
                        <div className="min-w-0">
                            <div className="text-[10px] text-muted-foreground uppercase tracking-wider mb-1">Goal Event</div>
                            <div className="text-xs font-medium truncate" title={mabGoalEvent || 'Any'}>{mabGoalEvent || 'Any'}</div>
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

            <ContextualRolloutManager
                displayContextual={displayContextual || []}
                isMabEnabled={isMabEnabled}
                canEditEnv={canEditEnv}
                displayRolloutPercentage={displayRolloutPercentage}
                historicalSnapshot={historicalSnapshot}
                setContextualRollout={setContextualRollout}
                deleteContextualRollout={deleteContextualRollout}
            />

            <ExperimentHistoryTable
                iterations={iterations || []}
                isLoadingIterations={isLoadingIterations}
                isHistoricalView={isHistoricalView}
                canEditEnv={canEditEnv}
                setHistoricalSnapshot={setHistoricalSnapshot}
                deleteMutation={deleteMutation}
            />
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
