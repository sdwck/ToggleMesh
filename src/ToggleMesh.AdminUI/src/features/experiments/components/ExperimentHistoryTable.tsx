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
}

export function ExperimentHistoryTable({ iterations, isLoadingIterations, isHistoricalView, canEditEnv, setHistoricalSnapshot, deleteMutation }: ExperimentHistoryTableProps) {
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
    );
}
