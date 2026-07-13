import { Card, CardContent } from '@/components/ui/card';
import { Activity, Zap } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';

export function MetricCard({ exp, isPrimary, variationsConfig, isBoolean }: { exp: any, isPrimary?: boolean, variationsConfig?: any[], isBoolean?: boolean }) {
    let variations = exp.variations || exp.Variations;

    if (!variations || variations.length === 0) {
        if (exp.controlExposures !== undefined || exp.ControlExposures !== undefined) {
            variations = [
                {
                    variationId: "Treatment (True)",
                    exposures: exp.treatmentExposures ?? exp.TreatmentExposures ?? 0,
                    conversions: exp.treatmentConversions ?? exp.TreatmentConversions ?? 0,
                    conversionRate: exp.treatmentConversionRate ?? exp.TreatmentConversionRate ?? 0,
                    arpu: exp.treatmentArpu ?? exp.TreatmentArpu ?? 0,
                    totalValue: exp.treatmentTotalValue ?? exp.TreatmentTotalValue ?? 0,
                    expectedUplift: exp.expectedUplift ?? exp.ExpectedUplift ?? 0,
                    probabilityToBeatBaseline: exp.probabilityToBeatBaseline ?? exp.ProbabilityToBeatBaseline ?? 0
                },
                {
                    variationId: "Control (False)",
                    exposures: exp.controlExposures ?? exp.ControlExposures ?? 0,
                    conversions: exp.controlConversions ?? exp.ControlConversions ?? 0,
                    conversionRate: exp.controlConversionRate ?? exp.ControlConversionRate ?? 0,
                    arpu: exp.controlArpu ?? exp.ControlArpu ?? 0,
                    totalValue: exp.controlTotalValue ?? exp.ControlTotalValue ?? 0,
                    expectedUplift: 0,
                    probabilityToBeatBaseline: 0
                }
            ];
        } else {
            variations = [];
        }
    }

    const sortedVariations = [...variations].sort((a, b) => (b.exposures || b.Exposures || 0) - (a.exposures || a.Exposures || 0));
    const baseline = sortedVariations[0];
    const nonBaseline = sortedVariations.slice(1);

    const getVariationName = (variationId: string) => {
        if (isBoolean) {
            const conf = variationsConfig?.find(v => v.id === variationId);
            return conf ? `Value: ${conf.value}` : variationId;
        }
        const conf = variationsConfig?.find(v => v.id === variationId);
        return conf ? conf.value : variationId;
    };

    return (
        <Card id={`track-${exp.eventName || exp.EventName}`} className={`border-border/40 overflow-hidden transition-all duration-1000 print:break-inside-avoid print:shadow-none print:border-zinc-300 ${isPrimary ? 'bg-primary/5 border-primary/20 print:bg-transparent' : 'bg-zinc-950/50 print:bg-transparent'}`}>
            <div className="px-4 py-3 bg-muted/20 border-b border-border/40 print:border-zinc-300 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
                <div className="flex flex-wrap items-center gap-2 min-w-0">
                    <Activity className={`h-4 w-4 shrink-0 ${isPrimary ? 'text-primary print:text-black' : 'text-muted-foreground print:text-zinc-600'}`} />
                    <span className={`font-semibold text-sm break-all ${isPrimary ? 'text-primary print:text-black' : 'print:text-zinc-800'}`}>{exp.eventName || exp.EventName} {isPrimary && '(Optimization Goal)'}</span>
                </div>
                <span className="text-xs text-muted-foreground print:text-zinc-500 whitespace-nowrap shrink-0">
                    Updated {(() => {
                        const raw = exp.lastCalculatedAt ?? exp.LastCalculatedAt;
                        if (!raw) return 'N/A';
                        const d = new Date(raw);
                        return isNaN(d.getTime()) ? 'N/A' : formatDistanceToNow(d, { addSuffix: true });
                    })()}
                </span>
            </div>
            <CardContent className="p-5 space-y-6">

                {baseline && (
                    <div className="space-y-4">
                        <div className="text-xs font-semibold text-muted-foreground tracking-wider uppercase">Baseline</div>
                        <div className="bg-zinc-900/50 print:bg-zinc-50 p-4 rounded border border-border/20 print:border-zinc-200 flex justify-between items-center">
                            <div>
                                <div className="text-sm font-medium mb-1">{getVariationName(baseline.variationId || baseline.VariationId)}</div>
                                <div className="text-xs text-muted-foreground/70 print:text-zinc-500">
                                    {baseline.conversions || baseline.Conversions || 0} / {baseline.exposures || baseline.Exposures || 0} users
                                </div>
                            </div>
                            <div className="text-right">
                                <div className="text-2xl font-bold font-mono print:text-black">
                                    {(((baseline.conversionRate ?? baseline.ConversionRate ?? ((baseline.exposures ?? baseline.Exposures ?? 0) > 0 ? (baseline.conversions ?? baseline.Conversions ?? 0) / (baseline.exposures ?? baseline.Exposures) : 0))) * 100).toFixed(1)}<span className="text-sm text-muted-foreground print:text-zinc-500 ml-1">%</span>
                                </div>
                                {(exp.isRevenueBased || exp.IsRevenueBased) && (
                                    <div className="text-xs text-muted-foreground print:text-zinc-500 mt-1">
                                        ARPU: <span className="font-mono text-zinc-400 print:text-zinc-600">${(baseline.arpu || baseline.Arpu || 0).toFixed(2)}</span>
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>
                )}

                {nonBaseline.length > 0 && (
                    <div className="space-y-4 pt-4 border-t border-border/20">
                        <div className="text-xs font-semibold text-muted-foreground tracking-wider uppercase">Comparisons vs Baseline</div>
                        <div className={`grid gap-4 ${nonBaseline.length > 1 ? 'grid-cols-1 md:grid-cols-2' : 'grid-cols-1'}`}>
                            {nonBaseline.map((variation: any, i: number) => {
                                const prob = Math.round((variation.probabilityToBeatBaseline || variation.ProbabilityToBeatBaseline || 0) * 100);
                                const upliftRaw = (variation.expectedUplift || variation.ExpectedUplift || 0) * 100;
                                const uplift = Math.round(upliftRaw);
                                const isPositive = upliftRaw > 0;
                                const isSignificant = prob >= 95 || prob <= 5;

                                const baselineExposures = baseline?.exposures || baseline?.Exposures || 0;
                                const variationExposures = variation.exposures || variation.Exposures || 0;
                                const variationConversions = variation.conversions || variation.Conversions || 0;
                                const baselineConversions = baseline?.conversions || baseline?.Conversions || 0;
                                const hasData = (baselineExposures + variationExposures >= 50) && (variationConversions > 0 || baselineConversions > 0);

                                return (
                                    <div key={i} className="space-y-3 bg-zinc-900/50 print:bg-zinc-50 p-4 rounded border border-border/20 print:border-zinc-200">
                                        <div className="flex justify-between items-start mb-2">
                                            <div>
                                                <div className="text-sm font-medium mb-1">{getVariationName(variation.variationId || variation.VariationId)}</div>
                                                <div className="text-xs text-muted-foreground/70 print:text-zinc-500">
                                                    {variation.conversions || variation.Conversions || 0} / {variation.exposures || variation.Exposures || 0} users
                                                </div>
                                            </div>
                                            <div className="text-right">
                                                <div className="text-xl font-bold font-mono print:text-black">
                                                    {(((variation.conversionRate ?? variation.ConversionRate ?? ((variation.exposures ?? variation.Exposures ?? 0) > 0 ? (variation.conversions ?? variation.Conversions ?? 0) / (variation.exposures ?? variation.Exposures) : 0))) * 100).toFixed(1)}<span className="text-sm text-muted-foreground print:text-zinc-500 ml-1">%</span>
                                                </div>
                                                {(exp.isRevenueBased || exp.IsRevenueBased) && (
                                                    <div className="text-xs text-muted-foreground print:text-zinc-500 mt-1">
                                                        ARPU: <span className="font-mono text-emerald-400 print:text-emerald-600">${(variation.arpu || variation.Arpu || 0).toFixed(2)}</span>
                                                    </div>
                                                )}
                                            </div>
                                        </div>

                                        <div className="pt-3 border-t border-border/10 space-y-3">
                                            <div className="flex items-center justify-between">
                                                <div className="flex items-center gap-1.5">
                                                    <Zap className={`h-3 w-3 ${isPositive ? 'text-emerald-500' : 'text-rose-500'}`} />
                                                    <span className="text-xs font-medium print:text-black">Uplift</span>
                                                </div>
                                                <span className={`text-xs font-bold ${isPositive ? 'text-emerald-500' : 'text-rose-500'}`}>
                                                    {isPositive ? '+' : ''}{uplift}%
                                                </span>
                                            </div>

                                            <div className="space-y-1.5">
                                                <div className="flex items-center justify-between text-xs">
                                                    <span className="text-muted-foreground print:text-zinc-500">Win Probability</span>
                                                    <span className={`font-mono font-bold ${hasData ? (isSignificant ? (prob >= 95 ? 'text-emerald-500' : 'text-rose-500') : 'print:text-zinc-800') : 'text-muted-foreground print:text-zinc-500'}`}>
                                                        {hasData ? `${prob}%` : 'N/A'}
                                                    </span>
                                                </div>
                                                <div className="relative h-1.5 w-full bg-secondary/30 print:bg-zinc-200 rounded-full overflow-hidden print:border print:border-zinc-300">
                                                    <div
                                                        className={`absolute top-0 left-0 h-full transition-all ${prob >= 95 ? 'bg-emerald-500 print:bg-emerald-500' : prob <= 5 ? 'bg-rose-500 print:bg-rose-500' : 'bg-primary print:bg-zinc-400'}`}
                                                        style={{ width: `${prob}%` }}
                                                    />
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                )}
            </CardContent>
        </Card>
    );
}
