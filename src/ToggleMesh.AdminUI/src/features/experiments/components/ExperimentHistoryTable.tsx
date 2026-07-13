import { format } from 'date-fns';
import { History, FileText, Trash2, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger } from "@/components/ui/alert-dialog";
import type { ExperimentIterationDto } from '@/api/types';
import type { UseMutationResult } from '@tanstack/react-query';

interface ExperimentHistoryTableProps {
    iterations: ExperimentIterationDto[];
    isLoadingIterations: boolean;
    isHistoricalView: boolean;
    canEditEnv: boolean;
    setHistoricalSnapshot: (snapshot: any) => void;
    deleteMutation: UseMutationResult<any, any, string, any>;
    currentHistoricalSnapshotId?: string;
}

export function ExperimentHistoryTable({ iterations, isLoadingIterations, isHistoricalView, canEditEnv, setHistoricalSnapshot, deleteMutation, currentHistoricalSnapshotId }: ExperimentHistoryTableProps) {
    if (isHistoricalView || isLoadingIterations || !iterations || iterations.length === 0) return null;

    return (
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
                                    if (!primary) {
                                        const exposureMetric = metrics.find((m: any) => (m.EventName || m.eventName) === '$exposure');
                                        if (exposureMetric) {
                                            primary = JSON.parse(JSON.stringify(exposureMetric));
                                            if (primary.EventName !== undefined) primary.EventName = iterationGoalEvent;
                                            if (primary.eventName !== undefined) primary.eventName = iterationGoalEvent;
                                            
                                            const vars = primary.Variations || primary.variations;
                                            if (vars && vars.length > 0) {
                                                for (const v of vars) {
                                                    if (v.Conversions !== undefined) v.Conversions = 0;
                                                    if (v.conversions !== undefined) v.conversions = 0;
                                                    if (v.ConversionRate !== undefined) v.ConversionRate = 0;
                                                    if (v.conversionRate !== undefined) v.conversionRate = 0;
                                                    if (v.ExpectedUplift !== undefined) v.ExpectedUplift = 0;
                                                    if (v.expectedUplift !== undefined) v.expectedUplift = 0;
                                                }
                                            } else {
                                                if (primary.ControlConversions !== undefined) primary.ControlConversions = 0;
                                                if (primary.controlConversions !== undefined) primary.controlConversions = 0;
                                                if (primary.TreatmentConversions !== undefined) primary.TreatmentConversions = 0;
                                                if (primary.treatmentConversions !== undefined) primary.treatmentConversions = 0;
                                                if (primary.ControlConversionRate !== undefined) primary.ControlConversionRate = 0;
                                                if (primary.controlConversionRate !== undefined) primary.controlConversionRate = 0;
                                                if (primary.TreatmentConversionRate !== undefined) primary.TreatmentConversionRate = 0;
                                                if (primary.treatmentConversionRate !== undefined) primary.treatmentConversionRate = 0;
                                                if (primary.ExpectedUplift !== undefined) primary.ExpectedUplift = 0;
                                                if (primary.expectedUplift !== undefined) primary.expectedUplift = 0;
                                            }
                                        }
                                    }
                                }
                                if (!primary) {
                                    const nonSystem = metrics.filter((m: any) => !(m.EventName || m.eventName).startsWith('$'));
                                    if (nonSystem.length > 0) {
                                        primary = [...nonSystem].sort((a: any, b: any) => {
                                            const getExps = (m: any) => {
                                                const vars = m.variations || m.Variations;
                                                if (vars && vars.length > 0) {
                                                    return vars.reduce((sum: number, v: any) => sum + (v.exposures ?? v.Exposures ?? 0), 0);
                                                }
                                                return (m.controlExposures ?? m.ControlExposures ?? 0) + (m.treatmentExposures ?? m.TreatmentExposures ?? 0);
                                            };
                                            return getExps(b) - getExps(a);
                                        })[0];
                                    }
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
                                        {(() => {
                                            if (!primary) return <span className="text-xs text-muted-foreground">No metric data</span>;

                                            const variations = primary.variations || primary.Variations || [];
                                            let baseline: any = null;
                                            let topPerformer: any = null;

                                            if (variations.length > 0) {
                                                baseline = [...variations].sort((a: any, b: any) => {
                                                    const expA = a.exposures ?? a.Exposures ?? 0;
                                                    const expB = b.exposures ?? b.Exposures ?? 0;
                                                    return expB - expA;
                                                })[0];

                                                const others = variations.filter((v: any) => (v.variationId || v.VariationId) !== (baseline.variationId || baseline.VariationId));
                                                if (others.length > 0) {
                                                    topPerformer = [...others].sort((a: any, b: any) => {
                                                        const upA = a.expectedUplift ?? a.ExpectedUplift ?? 0;
                                                        const upB = b.expectedUplift ?? b.ExpectedUplift ?? 0;
                                                        return upB - upA;
                                                    })[0];
                                                }
                                            }

                                            let baseCr = 0, baseExp = 0, baseName = "Baseline";
                                            let topCr = 0, topExp = 0, topName = "Top Performer";
                                            let uplift = 0;

                                            if (baseline) {
                                                baseExp = baseline.exposures ?? baseline.Exposures ?? 0;
                                                const baseConv = baseline.conversions ?? baseline.Conversions ?? 0;
                                                baseCr = baseExp > 0 ? baseConv / baseExp : 0;
                                            } else {
                                                baseExp = primary.controlExposures ?? primary.ControlExposures ?? 0;
                                                baseCr = primary.controlConversionRate ?? primary.ControlConversionRate ?? 0;
                                                baseName = "Control";
                                            }

                                            if (topPerformer) {
                                                topExp = topPerformer.exposures ?? topPerformer.Exposures ?? 0;
                                                const topConv = topPerformer.conversions ?? topPerformer.Conversions ?? 0;
                                                topCr = topExp > 0 ? topConv / topExp : 0;
                                                uplift = topPerformer.expectedUplift ?? topPerformer.ExpectedUplift ?? 0;
                                            } else {
                                                topExp = primary.treatmentExposures ?? primary.TreatmentExposures ?? 0;
                                                topCr = primary.treatmentConversionRate ?? primary.TreatmentConversionRate ?? 0;
                                                uplift = primary.expectedUplift ?? primary.ExpectedUplift ?? 0;
                                                topName = "Treatment";
                                            }

                                            return (
                                                <div className="flex items-center gap-4 text-xs">
                                                    <span className="font-medium text-foreground">{primary.EventName || primary.eventName}</span>
                                                    <span className="text-muted-foreground">
                                                        {baseName}: {(baseCr * 100).toFixed(1)}% ({baseExp})
                                                    </span>
                                                    <span className="text-muted-foreground">
                                                        {topName}: {(topCr * 100).toFixed(1)}% ({topExp})
                                                    </span>
                                                    <span className={`font-bold ${uplift > 0 ? 'text-emerald-500' : 'text-rose-500'}`}>
                                                        {uplift > 0 ? '+' : ''}{(uplift * 100).toFixed(1)}% uplift
                                                    </span>
                                                </div>
                                            );
                                        })()}
                                    </TableCell>
                                    <TableCell className='print:hidden'>
                                        <div className="flex items-center gap-2">
                                            <Button
                                                type="button"
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
                                                        type="button"
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
                                                            onClick={() => {
                                                                deleteMutation.mutate(iter.id, {
                                                                    onSuccess: () => {
                                                                        if (currentHistoricalSnapshotId === iter.id) {
                                                                            setHistoricalSnapshot(null);
                                                                        }
                                                                    }
                                                                });
                                                            }}
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
    );
}
