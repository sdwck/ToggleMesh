import { Card, CardContent } from "@/components/ui/card";
import { Sparkles, AlertTriangle, TrendingUp, HelpCircle, Activity } from "lucide-react";

export function InsightsWidget({ metric, isActive }: { metric: any, isActive: boolean }) {
    const variations = metric.variations || metric.Variations || [];
    const sortedVariations = [...variations].sort((a, b) => (b.exposures || b.Exposures || 0) - (a.exposures || a.Exposures || 0));

    if (sortedVariations.length === 0) {
        if (metric.controlExposures === undefined && metric.ControlExposures === undefined) return null;

        const cExp = metric.controlExposures ?? metric.ControlExposures ?? 0;
        const tExp = metric.treatmentExposures ?? metric.TreatmentExposures ?? 0;
        if (cExp < 100 || tExp < 100) return null;

        sortedVariations.push({
            exposures: cExp,
            arpu: metric.controlArpu ?? metric.ControlArpu ?? 0,
            expectedUplift: 0,
            probabilityToBeatBaseline: 0
        });
        sortedVariations.push({
            exposures: tExp,
            arpu: metric.treatmentArpu ?? metric.TreatmentArpu ?? 0,
            expectedUplift: metric.expectedUplift ?? metric.ExpectedUplift ?? 0,
            probabilityToBeatBaseline: metric.probabilityToBeatBaseline ?? metric.ProbabilityToBeatBaseline ?? 0
        });
    }

    if (sortedVariations.length < 2) return null;

    const baseline = sortedVariations[0];
    const comparisons = sortedVariations.slice(1);

    const totalExposures = sortedVariations.reduce((acc, v) => acc + (v.exposures || v.Exposures || 0), 0);
    if (totalExposures < 200) return null;

    const bestVariation = comparisons.reduce((best, current) => {
        const bestProb = best.probabilityToBeatBaseline || best.ProbabilityToBeatBaseline || 0;
        const currentProb = current.probabilityToBeatBaseline || current.ProbabilityToBeatBaseline || 0;
        return currentProb > bestProb ? current : best;
    }, comparisons[0]);

    const worstVariation = comparisons.reduce((worst, current) => {
        const worstProb = worst.probabilityToBeatBaseline || worst.ProbabilityToBeatBaseline || 0;
        const currentProb = current.probabilityToBeatBaseline || current.ProbabilityToBeatBaseline || 0;
        return currentProb < worstProb ? current : worst;
    }, comparisons[0]);

    const prob = Math.round((bestVariation.probabilityToBeatBaseline || bestVariation.ProbabilityToBeatBaseline || 0) * 100);
    const isSignificantWinner = prob >= 95;

    const worstProb = Math.round((worstVariation.probabilityToBeatBaseline || worstVariation.ProbabilityToBeatBaseline || 0) * 100);
    const isSignificantLoser = worstProb <= 5;

    const isRevenueBased = metric.isRevenueBased || metric.IsRevenueBased;
    const expectedUplift = bestVariation.expectedUplift || bestVariation.ExpectedUplift || 0;

    const upliftRaw = expectedUplift * 100;
    const upliftStr = Math.round(upliftRaw);

    const isNeutral = upliftStr === 0;

    let icon = <HelpCircle className="h-5 w-5 text-amber-500" />;
    let title = "Not enough data for conclusion";
    let message = "Continue testing. The results are not statistically significant yet.";
    let bgClass = "bg-amber-500/10 border-amber-500/20";
    let textClass = "text-amber-500";

    if (isNeutral && totalExposures > 1000) {
        icon = <Activity className="h-5 w-5 text-zinc-400" />;
        title = "NEUTRAL RESULTS";
        bgClass = "bg-zinc-800/50 border-zinc-700/50";
        textClass = "text-zinc-400";
        message = `The variations perform almost identically to the baseline (~0% uplift). ${isActive ? "You can keep it running to see if a trend emerges." : ""}`;
    } else if (isSignificantWinner) {
        icon = <Sparkles className="h-5 w-5 text-emerald-500" />;
        title = isActive ? "STRONGLY RECOMMEND ENABLING WINNER" : "SUCCESSFUL EXPERIMENT";
        bgClass = "bg-emerald-500/10 border-emerald-500/20";
        textClass = "text-emerald-500";

        if (isRevenueBased) {
            const extraRevenuePerUser = (bestVariation.arpu || bestVariation.Arpu || 0) - (baseline.arpu || baseline.Arpu || 0);
            const projectedRevenue = extraRevenuePerUser * 100000;
            message = `A variation significantly outperforms the baseline. Extrapolating this ARPU uplift (${upliftStr}%) to 100,000 users would generate an estimated $${projectedRevenue.toLocaleString(undefined, { maximumFractionDigits: 0 })} in additional revenue.`;
        } else {
            message = `A variation significantly outperforms the baseline. Rolling this out will likely increase ${metric.eventName || metric.EventName} conversions by ${upliftStr}%.`;
        }
    } else if (isSignificantLoser && comparisons.length === 1) {
        icon = <AlertTriangle className="h-5 w-5 text-rose-500" />;
        title = isActive ? "STRONGLY RECOMMEND DISABLING" : "TREATMENT UNDERPERFORMED";
        bgClass = "bg-rose-500/10 border-rose-500/20";
        textClass = "text-rose-500";

        if (isRevenueBased) {
            message = `The treatment ${isActive ? "is actively losing" : "lost"} revenue compared to the baseline. ${isActive ? "Turn this off." : ""}`;
        } else {
            message = `The treatment ${isActive ? "is actively reducing" : "reduced"} conversions by ${Math.abs(upliftStr)}% compared to the baseline. ${isActive ? "Turn this off." : ""}`;
        }
    } else if (totalExposures > 1000) {
        const isPositive = upliftRaw > 0;
        const metricType = isRevenueBased ? "revenue" : "conversion";

        icon = <TrendingUp className="h-5 w-5 text-blue-500" />;
        title = isActive ? `Trending towards ${isPositive ? "positive" : "negative"}` : "INCONCLUSIVE";
        bgClass = "bg-blue-500/10 border-blue-500/20";
        textClass = "text-blue-500";
        message = isActive
            ? `The experiment is leaning ${isPositive ? 'positive' : 'negative'} (${upliftStr > 0 ? '+' : ''}${upliftStr}% ${metricType} compared to baseline), but isn't statistically significant yet (prob: ${prob}%). Let it run longer.`
            : `The experiment was stopped before statistical significance was reached. Final data leaned ${isPositive ? 'positive' : 'negative'}, but remains inconclusive.`;
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
