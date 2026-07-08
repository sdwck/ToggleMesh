import { Activity, Lock, RotateCcw } from 'lucide-react';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger } from "@/components/ui/alert-dialog";
import type { UseMutationResult } from '@tanstack/react-query';

interface ContextualRolloutManagerProps {
    displayContextual: any[];
    isMabEnabled: boolean;
    canEditEnv: boolean;
    displayRolloutPercentage?: number;
    historicalSnapshot: any;
    setContextualRollout: UseMutationResult<any, any, { contextSlice: string; rolloutPercentage: number }, any>;
    deleteContextualRollout: UseMutationResult<any, any, string, any>;
}

export function ContextualRolloutManager({
    displayContextual,
    isMabEnabled,
    canEditEnv,
    displayRolloutPercentage,
    historicalSnapshot,
    setContextualRollout,
    deleteContextualRollout
}: ContextualRolloutManagerProps) {
    if (!displayContextual || displayContextual.length === 0) return null;

    return (
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
    );
}
