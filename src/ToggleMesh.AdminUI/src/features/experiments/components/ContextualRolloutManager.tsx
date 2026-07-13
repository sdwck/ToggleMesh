import { useState, useMemo } from 'react';
import { Activity, RotateCcw, ChevronDown, ChevronRight, Trophy } from 'lucide-react';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger } from "@/components/ui/alert-dialog";
import type { UseMutationResult } from '@tanstack/react-query';

interface ContextualRolloutManagerProps {
    displayContextual: any[];
    isMabEnabled: boolean;
    canEditEnv: boolean;
    historicalSnapshot: any;
    setContextualRollout?: any;
    deleteContextualRollout: UseMutationResult<any, any, string, any>;
    variationsConfig?: any[];
    isBoolean?: boolean;
}

export function ContextualRolloutManager({
    displayContextual,
    isMabEnabled,
    canEditEnv,
    historicalSnapshot,
    setContextualRollout,
    deleteContextualRollout,
    variationsConfig,
    isBoolean
}: ContextualRolloutManagerProps) {
    if (!displayContextual || displayContextual.length === 0) return null;
    const [showLowTraffic, setShowLowTraffic] = useState(false);

    const { highTrafficSlices, lowTrafficSlices, aggregatedIter, otherExposures } = useMemo(() => {
        const high = [];
        const low = [];
        let totalLowExposures = 0;
        let totalLowConversions = 0;

        for (const iter of (displayContextual || [])) {
            if (iter.contextSlice === '{}') {
                high.push(iter);
                continue;
            }
            
            const variations = iter.variations || iter.Variations || [];
            let totalExposures = 0;
            let totalConversions = 0;

            if (variations.length > 0) {
                for (const v of variations) {
                    totalExposures += (v.exposures ?? v.Exposures ?? 0);
                    totalConversions += (v.conversions ?? v.Conversions ?? 0);
                }
            } else {
                totalExposures = (iter.controlExposures ?? 0) + (iter.treatmentExposures ?? 0);
                totalConversions = (iter.controlConversions ?? 0) + (iter.treatmentConversions ?? 0);
            }

            iter._totalExposures = totalExposures;

            if (totalExposures < 50) {
                low.push(iter);
                totalLowExposures += totalExposures;
                totalLowConversions += totalConversions;
            } else {
                high.push(iter);
            }
        }

        high.sort((a, b) => {
            if (a.contextSlice === '{}') return -1;
            if (b.contextSlice === '{}') return 1;
            return (b._totalExposures ?? 0) - (a._totalExposures ?? 0);
        });

        low.sort((a, b) => (b._totalExposures ?? 0) - (a._totalExposures ?? 0));

        let aggregatedIter: any = null;
        if (low.length > 0) {
            const aggVars = new Map<string, any>();
            for (const slice of low) {
                const variations = slice.variations || slice.Variations || [];
                for (const v of variations) {
                    const vId = v.variationId || v.VariationId;
                    if (!aggVars.has(vId)) {
                        aggVars.set(vId, { 
                            variationId: vId,
                            VariationId: vId,
                            exposures: 0,
                            conversions: 0,
                            totalValue: 0,
                            probabilityToBeatBaseline: 0,
                            expectedUplift: 0,
                            rolloutWeight: undefined 
                        });
                    }
                    const agg = aggVars.get(vId)!;
                    agg.exposures += (v.exposures ?? v.Exposures ?? 0);
                    agg.conversions += (v.conversions ?? v.Conversions ?? 0);
                    agg.totalValue += (v.totalValue ?? v.TotalValue ?? 0);
                }
            }
            
            const aggVarsArray = Array.from(aggVars.values());
            if (aggVarsArray.length > 0) {
                const baseline = aggVarsArray[0];
                for (let i = 0; i < aggVarsArray.length; i++) {
                    const v = aggVarsArray[i];
                    v.conversionRate = v.exposures > 0 ? v.conversions / v.exposures : 0;
                    if (i > 0 && baseline.conversionRate > 0) {
                        v.expectedUplift = (v.conversionRate - baseline.conversionRate) / baseline.conversionRate;
                    }
                }
            }
            
            aggregatedIter = {
                contextSlice: 'low_traffic_aggregate',
                variations: aggVarsArray,
                isAutoManaged: true
            };
        }

        return { highTrafficSlices: high, lowTrafficSlices: low, aggregatedIter, otherExposures: totalLowExposures };
    }, [displayContextual]);

    const renderRow = (iter: any, i: number, isFaded: boolean = false, isAggregated: boolean = false) => {
        const variations = iter.variations || iter.Variations || [];
        
        const renderFirstCell = () => {
            if (isAggregated) {
                return (
                    <div className="flex items-center gap-2 font-medium text-xs">
                        {showLowTraffic ? <ChevronDown className="h-4 w-4 text-muted-foreground flex-shrink-0" /> : <ChevronRight className="h-4 w-4 text-muted-foreground flex-shrink-0" />}
                        <span>Low Traffic Slices ({lowTrafficSlices.length}) — {otherExposures} total exposures</span>
                    </div>
                );
            }
            
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

        const rowProps = isAggregated ? {
            onClick: () => setShowLowTraffic(!showLowTraffic),
            className: "border-border/40 bg-muted/20 cursor-pointer hover:bg-muted/40 transition-colors"
        } : {
            className: `border-border/40 ${isFaded ? 'opacity-50 hover:opacity-100 transition-opacity' : ''}`
        };

        return (
            <TableRow key={iter.contextSlice + i} {...rowProps}>
                <TableCell className={`text-xs px-2 pl-2 align-top ${isAggregated ? 'pt-2 pb-2 align-middle' : 'pt-4'}`}>
                    <div className={isAggregated ? "flex items-center" : "flex flex-wrap max-w-[150px]"}>
                        {renderFirstCell()}
                    </div>
                </TableCell>
                <TableCell className="px-2 pt-2 pb-2" colSpan={3}>
                    {variations.length > 0 ? (
                        <div className="space-y-2">
                            {variations.map((v: any, vIdx: number) => {
                                const varId = v.variationId || v.VariationId;
                                const config = variationsConfig?.find(c => c.id === varId);
                                const varName = config?.value || `Var ${vIdx + 1}`;
                                const rate = v.conversionRate ?? v.ConversionRate ?? 0;
                                const uplift = v.expectedUplift ?? v.ExpectedUplift ?? 0;
                                const winProb = v.probabilityToBeatBaseline ?? v.ProbabilityToBeatBaseline ?? 0;
                                const weight = v.rolloutWeight ?? v.RolloutWeight ?? 0;
                                
                                const baselineExposures = variations.length > 0 ? (variations[0].exposures ?? variations[0].Exposures ?? 0) : 0;
                                const baselineConversions = variations.length > 0 ? (variations[0].conversions ?? variations[0].Conversions ?? 0) : 0;
                                const variationExposures = v.exposures ?? v.Exposures ?? 0;
                                const variationConversions = v.conversions ?? v.Conversions ?? 0;
                                const hasData = (variationExposures + baselineExposures >= 50) && (variationConversions > 0 || baselineConversions > 0);
                                
                                const isWinner = winProb > 0.9 && hasData;
                                const isBaseline = uplift === 0 && winProb === 0 && vIdx === 0;

                                return (
                                    <div key={varId} className="flex items-center justify-between bg-muted/20 rounded px-3 py-2 text-xs border border-border/40">
                                        <div className="flex items-center gap-2 min-w-[100px]">
                                            {isWinner && <Trophy className="h-3 w-3 text-emerald-500" />}
                                            <span className="font-medium truncate max-w-[80px]" title={varName}>{varName}</span>
                                        </div>
                                        
                                        <div className="flex items-center gap-4 text-muted-foreground min-w-[150px]">
                                            <span className="w-16 text-right">{(rate * 100).toFixed(1)}% <span className="text-[10px] opacity-70">({v.exposures || v.Exposures || 0})</span></span>
                                            
                                            <span className={`w-16 text-right font-medium ${uplift > 0 ? 'text-emerald-500' : uplift < 0 ? 'text-rose-500' : 'text-muted-foreground'}`}>
                                                {isBaseline ? 'Baseline' : `${uplift > 0 ? '+' : ''}${(uplift * 100).toFixed(1)}%`}
                                            </span>
                                            
                                            <span className="w-24 text-right flex justify-end">
                                                {isAggregated ? (
                                                    <Badge variant="outline" className="text-[10px] h-5 px-1.5 font-mono text-muted-foreground border-dashed">
                                                        Mixed
                                                    </Badge>
                                                ) : isBoolean && varId === 'true' && setContextualRollout ? (
                                                    <div className="flex items-center gap-1 group">
                                                        <input
                                                            type="number"
                                                            defaultValue={(weight / 100).toFixed(0)}
                                                            disabled={!canEditEnv || setContextualRollout?.isPending || !!historicalSnapshot}
                                                            className={`peer w-[4ch] bg-transparent outline-none border-b border-dashed border-primary/40 hover:border-primary/80 focus:border-primary transition-colors text-right appearance-none [&::-webkit-inner-spin-button]:appearance-none font-mono text-[10px] font-semibold cursor-text text-primary/80 focus:text-primary ${!canEditEnv || setContextualRollout?.isPending || !!historicalSnapshot ? 'cursor-not-allowed opacity-50' : ''}`}
                                                            onBlur={(e) => {
                                                                if (!!historicalSnapshot) return;
                                                                let val = parseInt(e.target.value || '0', 10);
                                                                if (isNaN(val)) val = 0;
                                                                val = Math.max(0, Math.min(100, val));
                                                                
                                                                const oldVal = Math.round(weight / 100);
                                                                if (val !== oldVal && setContextualRollout) {
                                                                    setContextualRollout.mutate({ 
                                                                        contextSlice: iter.contextSlice, 
                                                                        rollout: [
                                                                            { variationId: 'true', weight: val * 100 },
                                                                            { variationId: 'false', weight: (100 - val) * 100 }
                                                                        ]
                                                                    });
                                                                } else {
                                                                    e.target.value = oldVal.toString();
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
                                                                    e.currentTarget.value = Math.round(weight / 100).toString();
                                                                    e.currentTarget.blur();
                                                                }
                                                            }}
                                                            title={canEditEnv && !historicalSnapshot ? "Click to edit rollout" : undefined}
                                                        />
                                                        <span className="text-muted-foreground text-[10px]">% traffic</span>
                                                    </div>
                                                ) : (
                                                    <Badge variant="outline" className="text-[10px] h-5 px-1.5 font-mono">
                                                        {(weight / 100).toFixed(1)}% traffic
                                                    </Badge>
                                                )}
                                            </span>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    ) : (
                        <div className="text-xs text-muted-foreground py-2">No variation data available</div>
                    )}
                </TableCell>
                
                <TableCell className="pr-2 pl-0 print:hidden align-top pt-4">
                    {iter.isAutoManaged === false && (
                        <div className="flex items-center gap-1 justify-end">
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
    };

    return (
        <div className="pt-8 border-t border-border/40 space-y-4 print:border-none">
            <h3 className="font-medium text-sm flex items-center gap-2">
                <Activity className="h-4 w-4 text-primary" />
                Discovered Contexts
            </h3>
            <p className="text-xs text-muted-foreground">
                {isMabEnabled
                    ? `ToggleMesh is automatically evaluating the MAB goal (${displayContextual[0]?.eventName || 'goal'}) across these context slices.`
                    : `Metrics broken down by discovered contexts (Goal: ${displayContextual[0]?.eventName || 'event'}). Rollout is global across all contexts.`}
            </p>

            <Table>
                <TableHeader>
                    <TableRow className="border-border/40 hover:bg-transparent">
                        <TableHead className="px-2 pl-2 w-[180px]">Context Slice</TableHead>
                        <TableHead className="px-2 pt-3 pb-3" colSpan={3}>
                            <div className="flex items-center justify-between px-3">
                                <div className="min-w-[100px]">Variation</div>
                                <div className="flex items-center gap-4 min-w-[150px]">
                                    <span className="w-16 text-right">Conv. Rate</span>
                                    <span className="w-16 text-right">Uplift</span>
                                    <span className="w-24 text-right">Traffic</span>
                                </div>
                            </div>
                        </TableHead>
                        <TableHead className="w-[40px] pr-2 pl-0 print:hidden"></TableHead>
                    </TableRow>
                </TableHeader>
                <TableBody>
                    {highTrafficSlices.map((iter: any, i: number) => renderRow(iter, i))}
                    
                    {lowTrafficSlices.length > 0 && (
                        <>
                            {aggregatedIter && renderRow(aggregatedIter, highTrafficSlices.length, false, true)}
                            {showLowTraffic && lowTrafficSlices.map((iter: any, i: number) => renderRow(iter, i + highTrafficSlices.length + 1, true))}
                        </>
                    )}
                </TableBody>
            </Table>
        </div>
    );
}
