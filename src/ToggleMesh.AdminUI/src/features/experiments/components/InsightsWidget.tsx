import { Card, CardContent } from "@/components/ui/card";
import { Sparkles, AlertTriangle, TrendingUp, HelpCircle, Activity } from "lucide-react";
import type { ExperimentResultDto } from "@/api/types";

export function InsightsWidget({ metric, isActive }: { metric: ExperimentResultDto, isActive: boolean }) {
    if (!metric || metric.controlExposures < 100 || metric.treatmentExposures < 100) return null;

    const prob = Math.round(metric.probabilityToBeatBaseline * 100);
    const isSignificantWinner = prob >= 95;
    const isSignificantLoser = prob <= 5;

    const conversionUplift = Math.round(metric.expectedUplift * 100);
    const isRevenueBased = metric.isRevenueBased;
    const revenueUplift = isRevenueBased ? Math.round(metric.expectedValueUplift * 100) : null;

    const isNeutral = isRevenueBased ? revenueUplift === 0 : conversionUplift === 0;

    let icon = <HelpCircle className="h-5 w-5 text-amber-500" />;
    let title = "Not enough data for conclusion";
    let message = "Continue testing. The results are not statistically significant yet.";
    let bgClass = "bg-amber-500/10 border-amber-500/20";
    let textClass = "text-amber-500";

    const hasContradiction = isRevenueBased && isSignificantWinner && revenueUplift !== null && revenueUplift < 0;

    if (hasContradiction) {
        icon = <AlertTriangle className="h-5 w-5 text-amber-500" />;
        title = isActive ? "CAUTION: REVENUE DROPPING" : "REVENUE DROPPED";
        bgClass = "bg-amber-500/10 border-amber-500/20";
        textClass = "text-amber-500";
        message = `Although the treatment increases conversion (by ${conversionUplift}%), it is actively losing revenue compared to the baseline. ARPU is down by ${Math.abs(revenueUplift!)}%. ${isActive ? "Consider turning this off." : "It was a good idea to stop this."}`;
    } else if (isNeutral) {
        icon = <Activity className="h-5 w-5 text-zinc-400" />;
        title = "NEUTRAL RESULTS";
        bgClass = "bg-zinc-800/50 border-zinc-700/50";
        textClass = "text-zinc-400";
        message = `The treatment performs exactly identically to the baseline (0% uplift). ${isActive ? "You can keep it running to see if a trend emerges." : ""}`;
    } else if (isSignificantWinner) {
        icon = <Sparkles className="h-5 w-5 text-emerald-500" />;
        title = isActive ? "STRONGLY RECOMMEND ENABLING" : "SUCCESSFUL EXPERIMENT";
        bgClass = "bg-emerald-500/10 border-emerald-500/20";
        textClass = "text-emerald-500";

        if (isRevenueBased && revenueUplift !== null && revenueUplift > 0) {
            const extraRevenuePerUser = metric.treatmentArpu - metric.controlArpu;
            const projectedRevenue = extraRevenuePerUser * 100000;
            message = `The treatment significantly outperforms the baseline. Extrapolating this ARPU uplift (${revenueUplift}%) to 100,000 users would generate an estimated $${projectedRevenue.toLocaleString(undefined, { maximumFractionDigits: 0 })} in additional revenue.`;
        } else {
            message = `The treatment significantly outperforms the baseline. Rolling this out will likely increase ${metric.eventName} conversions by ${conversionUplift}%.`;
        }
    } else if (isSignificantLoser) {
        icon = <AlertTriangle className="h-5 w-5 text-rose-500" />;
        title = isActive ? "STRONGLY RECOMMEND DISABLING" : "TREATMENT UNDERPERFORMED";
        bgClass = "bg-rose-500/10 border-rose-500/20";
        textClass = "text-rose-500";

        if (isRevenueBased && revenueUplift !== null && revenueUplift < 0) {
            message = `The treatment ${isActive ? "is actively losing" : "lost"} revenue compared to the baseline. ARPU ${isActive ? "is" : "was"} down by ${Math.abs(revenueUplift)}%. ${isActive ? "Turn this off." : ""}`;
        } else {
            message = `The treatment ${isActive ? "is actively reducing" : "reduced"} conversions by ${Math.abs(conversionUplift)}% compared to the baseline. ${isActive ? "Turn this off." : ""}`;
        }
    } else if (metric.controlExposures + metric.treatmentExposures > 1000) {
        const isPositive = isRevenueBased ? (revenueUplift !== null && revenueUplift > 0) : conversionUplift > 0;
        const upliftStr = isRevenueBased ? `${revenueUplift}%` : `${conversionUplift}%`;
        const metricType = isRevenueBased ? "revenue" : "conversion";

        icon = <TrendingUp className="h-5 w-5 text-blue-500" />;
        title = isActive ? `Trending towards ${isPositive ? "positive" : "negative"}` : "INCONCLUSIVE";
        bgClass = "bg-blue-500/10 border-blue-500/20";
        textClass = "text-blue-500";
        message = isActive 
            ? `The experiment is leaning ${isPositive ? 'positive' : 'negative'} (${upliftStr} ${metricType} uplift), but isn't statistically significant yet (prob: ${prob}%). Let it run longer.`
            : `The experiment was stopped before statistical significance was reached. Final data leaned ${isPositive ? 'positive' : 'negative'} (${upliftStr} uplift), but remains inconclusive.`;
    }

    if (!isActive && title === "Not enough data for conclusion") {
        title = "INCONCLUSIVE";
        message = "The experiment was stopped before statistical significance was reached.";
    }

    return (
        <Card className={`border ${bgClass} mb-6`}>
            <CardContent className="p-4 flex gap-4 items-start">
                <div className={`p-2 rounded-full bg-background/50 ${textClass}`}>
                    {icon}
                </div>
                <div>
                    <h4 className={`font-bold text-sm mb-1 ${textClass}`}>{!isActive && title !== "NEUTRAL RESULTS" && title !== "INCONCLUSIVE" ? "AI Insights (Historical):" : "AI Insights:"} {title}</h4>
                    <p className="text-sm text-zinc-300 leading-relaxed">
                        {message}
                    </p>
                </div>
            </CardContent>
        </Card>
    );
}
