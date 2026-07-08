import { Card, CardContent } from '@/components/ui/card';
import { Activity, Zap } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';

export function MetricCard({ exp, isPrimary }: { exp: any, isPrimary?: boolean }) {
    const prob = Math.round(exp.probabilityToBeatBaseline * 100);
    const uplift = exp.isRevenueBased ? Math.round(exp.expectedValueUplift * 100) : Math.round(exp.expectedUplift * 100);
    const isPositive = uplift > 0;
    const isSignificant = prob >= 95 || prob <= 5;

    return (
        <Card id={`track-${exp.eventName}`} className={`border-border/40 overflow-hidden transition-all duration-1000 print:break-inside-avoid print:shadow-none print:border-zinc-300 ${isPrimary ? 'bg-primary/5 border-primary/20 print:bg-transparent' : 'bg-zinc-950/50 print:bg-transparent'}`}>
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
