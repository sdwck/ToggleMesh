import { useState } from 'react';
import { useParams, useNavigate, useSearchParams } from 'react-router-dom';
import { useProjectExperiments, useProjectHistoricalExperiments } from '@/api/queries';
import { Card } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { ArrowLeft, Activity, Beaker, AlertTriangle, CheckCircle2, TrendingUp, TrendingDown, Star, ExternalLink, History } from 'lucide-react';
import { formatDistanceToNow, format } from 'date-fns';
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ExperimentResults } from './components/ExperimentResults';
import type { ProjectExperimentSummaryDto } from '@/api/types';
import { ProjectRole } from '@/api/types';
import { Button } from '@/components/ui/button';
import { EmptyState } from "@/components/EmptyState.tsx";
import { useProjectDetails } from '@/api/queries';

export function ProjectExperimentsPage() {
    const { projectId } = useParams<{ projectId: string }>();
    const navigate = useNavigate();

    const { data: experiments, isLoading } = useProjectExperiments(projectId!);
    const { data: historical, isLoading: isHistoricalLoading } = useProjectHistoricalExperiments(projectId!);
    const { data: project } = useProjectDetails(projectId!);

    const [selectedExp, setSelectedExp] = useState<ProjectExperimentSummaryDto | null>(null);
    const [searchParams, setSearchParams] = useSearchParams();
    const activeTab = searchParams.get('tab') || 'active';
    const setActiveTab = (val: string) => {
        setSearchParams(prev => {
            prev.set('tab', val);
            return prev;
        }, { replace: true });
    };

    if (isLoading) return <div className="p-8 text-center text-muted-foreground">Loading experiments...</div>;

    const hasNoExperiments = (!experiments || experiments.length === 0) && (!historical || historical.length === 0);

    if (hasNoExperiments) {
        return (
            <div className="flex flex-col items-center justify-center py-20 text-center">
                <div className="h-16 w-16 bg-muted/30 rounded-full flex items-center justify-center mb-4">
                    <Beaker className="h-8 w-8 text-muted-foreground" />
                </div>
                <h3 className="text-xl font-semibold tracking-tight">No Experiments Yet</h3>
                <p className="text-muted-foreground mt-2 max-w-sm">
                    Connect the SDK and track events to start seeing A/B test results across your feature flags.
                </p>
            </div>
        );
    }

    if (selectedExp) {
        return (
            <div className="space-y-6 animate-in fade-in slide-in-from-bottom-4 duration-300">
                <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4">
                    <div>
                        <Button variant="ghost" size="sm" onClick={() => setSelectedExp(null)} className="-ml-3 text-muted-foreground hover:text-foreground">
                            <ArrowLeft className="mr-2 h-4 w-4" />
                            Back to Experiments
                        </Button>
                        <h2 className="text-2xl font-bold tracking-tight mt-2 flex flex-wrap items-center gap-2 font-mono">
                            <span className="break-all">{selectedExp.flagKey}</span>
                            <Badge variant="outline" className="font-sans bg-zinc-900 shrink-0">
                                {selectedExp.environmentName}
                            </Badge>
                        </h2>
                    </div>
                    <Button
                        variant="outline"
                        size="sm"
                        className="shrink-0"
                        onClick={() => {
                            navigate(`/projects/${projectId}/flags/${selectedExp.flagKey}?envId=${selectedExp.environmentId}&tab=experiments&track=${selectedExp.eventName}`);
                            setSelectedExp(null);
                        }}
                    >
                        <ExternalLink className="h-4 w-4 mr-2" /> Open in Flag Settings
                    </Button>
                </div>

                <div className="">
                    <ExperimentResults
                        projectId={projectId!}
                        envId={selectedExp.environmentId}
                        flagKey={selectedExp.flagKey}
                        mabGoalEvent={(!selectedExp.isExperimentActive && selectedExp.totalParticipants === 0) ? selectedExp.eventName : (selectedExp.isPrimaryGoal ? selectedExp.eventName : null)}
                        highlightTrack={selectedExp.eventName}
                        isExperimentActive={selectedExp.isExperimentActive}
                        isMabEnabled={selectedExp.isMabEnabled}
                        rolloutPercentage={selectedExp.rolloutPercentage ?? undefined}
                        disableScroll={true}
                        onStopSuccess={() => setSelectedExp(null)}
                        isHistoricalView={!selectedExp.isExperimentActive && selectedExp.totalParticipants === 0 && selectedExp.expectedUplift === 0 && !selectedExp.isPrimaryGoal && selectedExp.rolloutPercentage === 100}
                        initialHistoricalSnapshot={(selectedExp as any)._historicalSnapshot}
                        canEditEnv={project?.environments?.find(e => e.id === selectedExp.environmentId)?.userRole === ProjectRole.Owner || project?.environments?.find(e => e.id === selectedExp.environmentId)?.userRole === ProjectRole.Admin || project?.environments?.find(e => e.id === selectedExp.environmentId)?.userRole === ProjectRole.Editor}
                    />
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h2 className="text-2xl font-bold tracking-tight">Experiment Dashboard</h2>
                    <p className="text-muted-foreground">
                        Monitor active A/B testing results and review historical performance.
                    </p>
                </div>
            </div>

            <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
                <TabsList className="bg-zinc-950 border border-border/40 p-1 mb-4">
                    <TabsTrigger value="active" className="text-xs gap-1.5">
                        <Activity className="h-3.5 w-3.5" /> Active Experiments
                    </TabsTrigger>
                    <TabsTrigger value="historical" className="text-xs gap-1.5">
                        <History className="h-3.5 w-3.5" /> Historical Results
                    </TabsTrigger>
                </TabsList>

                <TabsContent value="active">
                    <Card className="border-border/40 overflow-hidden">
                        <Table>
                            <TableHeader className="bg-muted/30">
                                <TableRow className="border-border/40 hover:bg-transparent">
                                    <TableHead>Feature Flag</TableHead>
                                    <TableHead className="hidden sm:table-cell">Environment</TableHead>
                                    <TableHead className="hidden lg:table-cell">Goal Event</TableHead>
                                    <TableHead>Result</TableHead>
                                    <TableHead className="text-right hidden md:table-cell">Participants</TableHead>
                                    <TableHead className="text-right hidden sm:table-cell">Updated</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {(!experiments || experiments.filter(e => e.eventName && !e.eventName.startsWith('$')).length === 0) ? (
                                    <TableRow>
                                        <TableCell colSpan={6} className="p-0">
                                            <EmptyState
                                                icon={Activity}
                                                title="No Active Experiments"
                                                description="There are currently no active A/B tests running. Start one from a feature flag to begin collecting data."
                                            />
                                        </TableCell>
                                    </TableRow>
                                ) : (
                                    experiments.filter(e => e.eventName && !e.eventName.startsWith('$')).map((exp, index) => {
                                        const prob = Math.round(exp.probabilityToBeatBaseline * 100);
                                        const uplift = exp.isRevenueBased
                                            ? Math.round(exp.expectedValueUplift * 100)
                                            : Math.round(exp.expectedUplift * 100);
                                        const isPositive = uplift > 0;
                                        const isSignificant = prob >= 95 || prob <= 5;

                                        return (
                                            <TableRow
                                                key={(exp as any).id || index}
                                                className="border-border/40 hover:bg-muted/30 cursor-pointer transition-colors"
                                                onClick={() => setSelectedExp(exp)}
                                            >
                                                <TableCell className="font-medium font-mono">
                                                    <div className="flex flex-col gap-1">
                                                        <span>{exp.flagKey}</span>
                                                        <span className="sm:hidden text-xs text-muted-foreground bg-zinc-900/50 w-fit px-1.5 rounded">{exp.environmentName}</span>
                                                    </div>
                                                </TableCell>
                                                <TableCell className="hidden sm:table-cell">
                                                    <Badge variant="outline" className="font-normal bg-zinc-900/50">
                                                        {exp.environmentName}
                                                    </Badge>
                                                </TableCell>
                                                <TableCell className="hidden lg:table-cell">
                                                    <div className="flex items-center gap-1.5">
                                                        {exp.isPrimaryGoal ? (
                                                            <Star className="h-3.5 w-3.5 text-amber-500 fill-amber-500/20 shrink-0" />
                                                        ) : (
                                                            <Activity className="h-3.5 w-3.5 text-primary shrink-0" />
                                                        )}
                                                        <span className="font-medium truncate max-w-[150px]">{exp.eventName}</span>
                                                        {exp.isPrimaryGoal && (
                                                            <Badge variant="secondary" className="bg-primary/10 text-primary text-[9px] px-1 h-4 ml-1">PRIMARY</Badge>
                                                        )}
                                                    </div>
                                                </TableCell>
                                                <TableCell>
                                                    {exp.totalParticipants < 100 ? (
                                                        <Badge variant="outline" className="bg-zinc-800/40 text-zinc-400 border-zinc-700/50">
                                                            <Activity className="mr-1 h-3 w-3" /> Collecting Data
                                                        </Badge>
                                                    ) : isSignificant ? (
                                                        isPositive ? (
                                                            <Badge variant="outline" className="bg-emerald-500/10 text-emerald-500 border-emerald-500/30">
                                                                <CheckCircle2 className="mr-1 h-3 w-3" /> Winner: Treatment
                                                            </Badge>
                                                        ) : (
                                                            <Badge variant="outline" className="bg-rose-500/10 text-rose-500 border-rose-500/30">
                                                                <AlertTriangle className="mr-1 h-3 w-3" /> Winner: Control
                                                            </Badge>
                                                        )
                                                    ) : (
                                                        <div className="flex items-center gap-2">
                                                            <Badge variant="outline" className="bg-amber-500/10 text-amber-500 border-amber-500/30">
                                                                Inconclusive ({prob}%)
                                                            </Badge>
                                                        </div>
                                                    )}
                                                    {exp.totalParticipants >= 100 && (
                                                        <div className={`text-[11px] font-medium mt-1.5 flex items-center gap-1 ${isPositive ? 'text-emerald-500/80' : 'text-rose-500/80'}`}>
                                                            {isPositive ? <TrendingUp className="h-3 w-3" /> : <TrendingDown className="h-3 w-3" />}
                                                            {isPositive ? '+' : ''}{uplift}% {exp.isRevenueBased ? 'Rev. Uplift' : 'Uplift'}
                                                        </div>
                                                    )}
                                                </TableCell>
                                                <TableCell className="text-right font-mono hidden md:table-cell">
                                                    {exp.totalParticipants.toLocaleString()}
                                                </TableCell>
                                                <TableCell className="text-right text-muted-foreground text-sm hidden sm:table-cell whitespace-nowrap">
                                                    {formatDistanceToNow(new Date(exp.lastCalculatedAt), { addSuffix: true })}
                                                </TableCell>
                                            </TableRow>
                                        );
                                    }))}
                            </TableBody>
                        </Table>
                    </Card>
                </TabsContent>

                <TabsContent value="historical">
                    <Card className="border-border/40 overflow-hidden">
                        <Table>
                            <TableHeader className="bg-muted/30">
                                <TableRow className="border-border/40 hover:bg-transparent">
                                    <TableHead>Feature Flag</TableHead>
                                    <TableHead className="hidden sm:table-cell">Environment</TableHead>
                                    <TableHead className="hidden lg:table-cell">Goal Event</TableHead>
                                    <TableHead>Final Result</TableHead>
                                    <TableHead className="text-right hidden md:table-cell">Ended</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {isHistoricalLoading && (
                                    <TableRow>
                                        <TableCell colSpan={5} className="text-center py-8 text-muted-foreground">
                                            Loading historical experiments...
                                        </TableCell>
                                    </TableRow>
                                )}
                                {!isHistoricalLoading && historical?.length === 0 && (
                                    <TableRow>
                                        <TableCell colSpan={5} className="text-center py-8 text-muted-foreground">
                                            No completed experiments yet. Stop an active experiment to see it here.
                                        </TableCell>
                                    </TableRow>
                                )}
                                {historical?.map((iter) => {
                                    const snapshot = iter.finalMetricsSnapshot ? JSON.parse(iter.finalMetricsSnapshot) : null;
                                    const configSnapshot = iter.flagConfigSnapshot ? JSON.parse(iter.flagConfigSnapshot) : {};
                                    const iterationGoalEvent = configSnapshot.MabGoalEvent || configSnapshot.mabGoalEvent;

                                    const metrics = Array.isArray(snapshot?.Global) ? snapshot.Global : Array.isArray(snapshot) ? snapshot : [];
                                    let primary: any = null;
                                    let baseCr = 0, topCr = 0, uplift = 0, baseName = 'A', topName = 'B';

                                    if (Array.isArray(metrics) && metrics.length > 0) {
                                        if (iterationGoalEvent) {
                                            primary = metrics.find((m: any) => (m.EventName || m.eventName) === iterationGoalEvent);
                                            if (!primary) {
                                                const exposureMetric = metrics.find((m: any) => (m.EventName || m.eventName) === '$exposure');
                                                if (exposureMetric) {
                                                    primary = JSON.parse(JSON.stringify(exposureMetric));
                                                    if (primary.EventName !== undefined) primary.EventName = iterationGoalEvent;
                                                    if (primary.eventName !== undefined) primary.eventName = iterationGoalEvent;
                                                }
                                            }
                                        }
                                        if (!primary) {
                                            const nonSystem = metrics.filter((m: any) => !(m.EventName || m.eventName).startsWith('$'));
                                            const sortByExp = (arr: any[]) => [...arr].sort((a: any, b: any) => {
                                                const varsA = a.variations || a.Variations;
                                                const expA = varsA && varsA.length ? varsA.reduce((s: number, v: any) => s + (v.exposures ?? v.Exposures ?? 0), 0) : (a.ControlExposures || a.controlExposures || 0) + (a.TreatmentExposures || a.treatmentExposures || 0);
                                                const varsB = b.variations || b.Variations;
                                                const expB = varsB && varsB.length ? varsB.reduce((s: number, v: any) => s + (v.exposures ?? v.Exposures ?? 0), 0) : (b.ControlExposures || b.controlExposures || 0) + (b.TreatmentExposures || b.treatmentExposures || 0);
                                                return expB - expA;
                                            });
                                            primary = nonSystem.length > 0 ? sortByExp(nonSystem)[0] : sortByExp(metrics)[0];
                                        }

                                        if (primary) {
                                            const variations = primary.variations || primary.Variations || [];
                                            if (variations.length > 0) {
                                                const baseline = [...variations].sort((a: any, b: any) => (b.exposures ?? b.Exposures ?? 0) - (a.exposures ?? a.Exposures ?? 0))[0];
                                                const others = variations.filter((v: any) => (v.variationId || v.VariationId) !== (baseline.variationId || baseline.VariationId));
                                                const topPerformer = others.length > 0 ? [...others].sort((a: any, b: any) => (b.expectedUplift ?? b.ExpectedUplift ?? 0) - (a.expectedUplift ?? a.ExpectedUplift ?? 0))[0] : null;

                                                const baseExp = baseline.exposures ?? baseline.Exposures ?? 0;
                                                const baseConv = baseline.conversions ?? baseline.Conversions ?? 0;
                                                baseCr = baseExp > 0 ? baseConv / baseExp : 0;

                                                if (topPerformer) {
                                                    const topExp = topPerformer.exposures ?? topPerformer.Exposures ?? 0;
                                                    const topConv = topPerformer.conversions ?? topPerformer.Conversions ?? 0;
                                                    topCr = topExp > 0 ? topConv / topExp : 0;
                                                    uplift = topPerformer.expectedUplift ?? topPerformer.ExpectedUplift ?? 0;
                                                }
                                                baseName = "Baseline";
                                                topName = "Top";
                                            } else {
                                                baseCr = primary.ControlConversionRate ?? primary.controlConversionRate ?? 0;
                                                topCr = primary.TreatmentConversionRate ?? primary.treatmentConversionRate ?? 0;
                                                uplift = primary.ExpectedUplift ?? primary.expectedUplift ?? 0;
                                            }

                                            if (!metrics.find((m: any) => m === primary)) {
                                                baseCr = 0; topCr = 0; uplift = 0;
                                            }
                                        }
                                    }

                                    const displayGoalEvent = iterationGoalEvent || (primary ? (primary.EventName || primary.eventName) : 'Unknown');

                                    return (
                                        <TableRow
                                            key={iter.id}
                                            className="border-border/40 hover:bg-muted/30 cursor-pointer transition-colors"
                                            onClick={() => {
                                                setSelectedExp({
                                                    environmentId: iter.environmentId,
                                                    environmentName: iter.environmentName,
                                                    flagKey: iter.flagKey,
                                                    eventName: displayGoalEvent,
                                                    totalParticipants: 0,
                                                    lastCalculatedAt: iter.endedAt,
                                                    probabilityToBeatBaseline: 0,
                                                    expectedUplift: 0,
                                                    expectedValueUplift: 0,
                                                    isRevenueBased: false,
                                                    isPrimaryGoal: false,
                                                    isExperimentActive: false,
                                                    isMabEnabled: false,
                                                    rolloutPercentage: 100,
                                                    _historicalSnapshot: iter
                                                } as any);
                                            }}
                                        >
                                            <TableCell className="font-medium font-mono">
                                                <div className="flex flex-col gap-1">
                                                    <span>{iter.flagKey}</span>
                                                    <span className="sm:hidden text-xs text-muted-foreground bg-zinc-900/50 w-fit px-1.5 rounded">{iter.environmentName}</span>
                                                </div>
                                            </TableCell>
                                            <TableCell className="hidden sm:table-cell">
                                                <Badge variant="outline" className="font-normal bg-zinc-900/50">
                                                    {iter.environmentName}
                                                </Badge>
                                            </TableCell>
                                            <TableCell className="hidden lg:table-cell">
                                                {displayGoalEvent !== 'Unknown' ? (
                                                    <div className="flex items-center gap-1.5">
                                                        <Activity className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
                                                        <span className="font-medium text-foreground truncate max-w-[150px]">{displayGoalEvent}</span>
                                                    </div>
                                                ) : (
                                                    <span className="text-muted-foreground italic">Unknown</span>
                                                )}
                                            </TableCell>
                                            <TableCell>
                                                {primary ? (
                                                    <div className="flex flex-col gap-1 text-xs">
                                                        <span className={`font-bold ${uplift > 0 ? 'text-emerald-500' : uplift < 0 ? 'text-rose-500' : 'text-muted-foreground'}`}>
                                                            {uplift > 0 ? '+' : ''}{(uplift * 100).toFixed(1)}% uplift
                                                        </span>
                                                        <div className="hidden sm:flex items-center gap-2">
                                                            <span className="text-muted-foreground">
                                                                {baseName}: {(baseCr * 100).toFixed(1)}%
                                                            </span>
                                                            <span className="text-muted-foreground">
                                                                {topName}: {(topCr * 100).toFixed(1)}%
                                                            </span>
                                                        </div>
                                                    </div>
                                                ) : (
                                                    <span className="text-muted-foreground text-xs">No metric data</span>
                                                )}
                                            </TableCell>
                                            <TableCell className="text-right text-muted-foreground text-xs hidden md:table-cell whitespace-nowrap">
                                                {format(new Date(iter.endedAt), 'MMM d, yyyy')}
                                            </TableCell>
                                        </TableRow>
                                    );
                                })}
                            </TableBody>
                        </Table>
                    </Card>
                </TabsContent>
            </Tabs>

        </div>
    );
}
