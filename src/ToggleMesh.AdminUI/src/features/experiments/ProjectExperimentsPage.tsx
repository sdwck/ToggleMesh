import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useProjectExperiments, useProjectHistoricalExperiments } from '@/api/queries';
import { Card } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Activity, Beaker, AlertTriangle, CheckCircle2, TrendingUp, TrendingDown, Star, ExternalLink, History } from 'lucide-react';
import { formatDistanceToNow, format } from 'date-fns';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription } from '@/components/ui/sheet';
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

            <Tabs defaultValue="active" className="w-full">
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
                                    <TableHead>Environment</TableHead>
                                    <TableHead>Goal Event</TableHead>
                                    <TableHead>Result</TableHead>
                                    <TableHead className="text-right">Participants</TableHead>
                                    <TableHead className="text-right">Last Updated</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                            {(!experiments || experiments.length === 0) ? (
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
                                experiments.map((exp, index) => {
                                    const prob = Math.round(exp.probabilityToBeatBaseline * 100);
                                    const uplift = exp.isRevenueBased 
                                        ? Math.round(exp.expectedValueUplift * 100) 
                                        : Math.round(exp.expectedUplift * 100);
                                    const isPositive = uplift > 0;
                                    const isSignificant = prob >= 95 || prob <= 5;
                                
                                return (
                                    <TableRow 
                                        key={index} 
                                        className="border-border/40 hover:bg-muted/30 cursor-pointer transition-colors"
                                        onClick={() => setSelectedExp(exp)}
                                    >
                                        <TableCell className="font-medium font-mono">
                                            {exp.flagKey}
                                        </TableCell>
                                        <TableCell>
                                            <Badge variant="outline" className="font-normal bg-zinc-900/50">
                                                {exp.environmentName}
                                            </Badge>
                                        </TableCell>
                                        <TableCell>
                                            <div className="flex items-center gap-1.5">
                                                {exp.isPrimaryGoal ? (
                                                    <Star className="h-3.5 w-3.5 text-amber-500 fill-amber-500/20" />
                                                ) : (
                                                    <Activity className="h-3.5 w-3.5 text-primary" />
                                                )}
                                                <span className="font-medium">{exp.eventName}</span>
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
                                        <TableCell className="text-right font-mono">
                                            {exp.totalParticipants.toLocaleString()}
                                        </TableCell>
                                        <TableCell className="text-right text-muted-foreground text-sm">
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
                                    <TableHead>Environment</TableHead>
                                    <TableHead>Goal Event</TableHead>
                                    <TableHead>Final Result</TableHead>
                                    <TableHead className="text-right">Ended</TableHead>
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
                                    let primary = null;
                                    
                                    if (Array.isArray(metrics) && metrics.length > 0) {
                                        if (iterationGoalEvent) {
                                            primary = metrics.find((m: any) => (m.EventName || m.eventName) === iterationGoalEvent);
                                        }
                                        if (!primary) {
                                            primary = [...metrics].sort((a: any, b: any) => (b.ControlExposures || 0) - (a.ControlExposures || 0))[0];
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
                                                {iter.flagKey}
                                            </TableCell>
                                            <TableCell>
                                                <Badge variant="outline" className="font-normal bg-zinc-900/50">
                                                    {iter.environmentName}
                                                </Badge>
                                            </TableCell>
                                            <TableCell>
                                                {displayGoalEvent !== 'Unknown' ? (
                                                    <div className="flex items-center gap-1.5">
                                                        <Activity className="h-3.5 w-3.5 text-muted-foreground" />
                                                        <span className="font-medium text-foreground">{displayGoalEvent}</span>
                                                    </div>
                                                ) : (
                                                    <span className="text-muted-foreground italic">Unknown</span>
                                                )}
                                            </TableCell>
                                            <TableCell>
                                                {primary ? (
                                                    <div className="flex items-center gap-4 text-xs">
                                                        <span className="text-muted-foreground">
                                                            A: {(primary.ControlConversionRate * 100).toFixed(1)}% ({primary.ControlExposures})
                                                        </span>
                                                        <span className="text-muted-foreground">
                                                            B: {(primary.TreatmentConversionRate * 100).toFixed(1)}% ({primary.TreatmentExposures})
                                                        </span>
                                                        <span className={`font-bold ${primary.ExpectedUplift > 0 ? 'text-emerald-500' : 'text-rose-500'}`}>
                                                            {primary.ExpectedUplift > 0 ? '+' : ''}{(primary.ExpectedUplift * 100).toFixed(1)}% uplift
                                                        </span>
                                                    </div>
                                                ) : (
                                                    <span className="text-muted-foreground text-xs">No metric data</span>
                                                )}
                                            </TableCell>
                                            <TableCell className="text-right text-muted-foreground text-sm">
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

            <Sheet open={!!selectedExp} onOpenChange={(open) => !open && setSelectedExp(null)}>
                <SheetContent className="sm:max-w-xl md:max-w-2xl overflow-y-auto bg-zinc-950/95 border-l-border/40 backdrop-blur-xl">
                    {selectedExp && (
                        <>
                            <SheetHeader className="mb-6 pb-6 border-b border-border/40">
                                <div className="flex items-start justify-between pr-8">
                                    <div>
                                        <SheetTitle className="text-xl font-bold flex items-center gap-2 font-mono">
                                            {selectedExp.flagKey}
                                            <Badge variant="outline" className="ml-2 font-sans bg-zinc-900">
                                                {selectedExp.environmentName}
                                            </Badge>
                                        </SheetTitle>
                                        <SheetDescription className="mt-2">
                                            Detailed experiment results for {selectedExp.eventName}.
                                        </SheetDescription>
                                    </div>
                                    <Button 
                                        variant="outline" 
                                        size="sm"
                                        className="h-8 gap-1.5"
                                        onClick={() => {
                                            navigate(`/projects/${projectId}/flags/${selectedExp.flagKey}?envId=${selectedExp.environmentId}&tab=experiments&track=${selectedExp.eventName}`);
                                            setSelectedExp(null);
                                        }}
                                    >
                                        <ExternalLink className="h-3.5 w-3.5" /> Open Flag
                                    </Button>
                                </div>
                            </SheetHeader>
                            <div className="mt-4">
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
                        </>
                    )}
                </SheetContent>
            </Sheet>
        </div>
    );
}
