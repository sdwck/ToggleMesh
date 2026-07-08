import { useState, useRef, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useProjectDashboard } from '@/api/queries';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Activity, AlertTriangle, Network, Flag, TrendingUp, ArrowRight, Sparkles, BarChart2 } from 'lucide-react';
import { format } from 'date-fns';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
const CHART_MARGIN = { top: 5, right: 10, left: 10, bottom: 0 };

export function ProjectDashboardPage() {
    const { projectId } = useParams<{ projectId: string }>();
    const navigate = useNavigate();
    const [selectedEnv] = useState<string>(() => {
        return localStorage.getItem(`togglemesh_dashboard_env_${projectId}`) || 'all';
    });

    const { data: dashboard, isLoading } = useProjectDashboard(projectId!, selectedEnv === 'all' ? undefined : selectedEnv);

    const mountTime = useRef(Date.now());
    const [chartData, setChartData] = useState(() => {
        if (!dashboard) return [];
        return dashboard.evaluationsLast24Hours.map(pt => ({
            time: format(new Date(pt.time), 'HH:mm'),
            count: pt.count,
            fullDate: new Date(pt.time)
        }));
    });

    useEffect(() => {
        if (!dashboard) return;

        const newData = dashboard.evaluationsLast24Hours.map(pt => ({
            time: format(new Date(pt.time), 'HH:mm'),
            count: pt.count,
            fullDate: new Date(pt.time)
        }));

        const timeSinceMount = Date.now() - mountTime.current;
        if (timeSinceMount < 1600) {
            const timeout = setTimeout(() => {
                setChartData(newData);
            }, 1600 - timeSinceMount);
            return () => clearTimeout(timeout);
        } else {
            setChartData(newData);
        }
    }, [JSON.stringify(dashboard?.evaluationsLast24Hours)]);

    if (isLoading || !dashboard) {
        return (
            <div className="space-y-6">
                <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                    {[1, 2, 3, 4].map(i => <Skeleton key={i} className="h-[120px] w-full rounded-xl" />)}
                </div>
                <Skeleton className="h-[400px] w-full rounded-xl" />
            </div>
        );
    }

    const totalEvaluations = dashboard.evaluationsLast24Hours.reduce((acc, point) => acc + point.count, 0);

    return (
        <div className="space-y-4 -mt-2 animate-in fade-in-50 duration-500">
            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-4">
                <Card className="bg-zinc-950/50 border-border/40 hover:bg-zinc-900/50 transition-colors">
                    <CardHeader className="flex flex-row items-center justify-between pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Active Flags</CardTitle>
                        <Flag className="h-4 w-4 text-emerald-500" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-3xl font-bold font-mono">{dashboard.activeFlagsCount}</div>
                        <p className="text-xs text-muted-foreground mt-1">
                            Enabled rules across all environments
                        </p>
                    </CardContent>
                </Card>

                <Card className="bg-zinc-950/50 border-border/40 hover:bg-zinc-900/50 transition-colors">
                    <CardHeader className="flex flex-row items-center justify-between pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">AI Managed Flags</CardTitle>
                        <Sparkles className="h-4 w-4 text-purple-500" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-3xl font-bold font-mono">{dashboard.mabActiveFlagsCount ?? 0}</div>
                        <p className="text-xs text-muted-foreground mt-1">
                            Dynamic rollout (MAB) active
                        </p>
                    </CardContent>
                </Card>

                <Card className="bg-zinc-950/50 border-border/40 hover:bg-zinc-900/50 transition-colors">
                    <CardHeader className="flex flex-row items-center justify-between pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">Environments</CardTitle>
                        <Network className="h-4 w-4 text-blue-500" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-3xl font-bold font-mono">{dashboard.environmentsCount}</div>
                        <p className="text-xs text-muted-foreground mt-1">
                            Configured deployment targets
                        </p>
                    </CardContent>
                </Card>

                <Card className="bg-zinc-950/50 border-border/40 hover:bg-zinc-900/50 transition-colors">
                    <CardHeader className="flex flex-row items-center justify-between pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">SDK Evaluations</CardTitle>
                        <Activity className="h-4 w-4 text-primary" />
                    </CardHeader>
                    <CardContent>
                        <div className="text-3xl font-bold font-mono">
                            {totalEvaluations > 1000000
                                ? `${(totalEvaluations / 1000000).toFixed(1)}m`
                                : totalEvaluations > 1000
                                    ? `${(totalEvaluations / 1000).toFixed(1)}k`
                                    : totalEvaluations}
                        </div>
                        <p className="text-xs text-muted-foreground mt-1">
                            Total flag evaluations in last 24h
                        </p>
                    </CardContent>
                </Card>

                {dashboard.failingWebhooksCount != null && (
                    <Card className={`bg-zinc-950/50 transition-colors ${dashboard.failingWebhooksCount > 0 ? 'border-rose-500/50' : 'border-border/40 hover:bg-zinc-900/50'}`}>
                        <CardHeader className="flex flex-row items-center justify-between pb-2">
                            <CardTitle className="text-sm font-medium text-muted-foreground">Failing Webhooks</CardTitle>
                            <AlertTriangle className={`h-4 w-4 ${dashboard.failingWebhooksCount > 0 ? 'text-rose-500' : 'text-zinc-500'}`} />
                        </CardHeader>
                        <CardContent>
                            <div className={`text-3xl font-bold font-mono ${dashboard.failingWebhooksCount > 0 ? 'text-rose-500' : ''}`}>
                                {dashboard.failingWebhooksCount}
                            </div>
                            <p className="text-xs text-muted-foreground mt-1">
                                {dashboard.failingWebhooksCount > 0 ? 'Requires immediate attention' : 'All integration endpoints healthy'}
                            </p>
                        </CardContent>
                    </Card>
                )}
            </div>

            <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
                <Card className="xl:col-span-2 border-border/40 bg-zinc-950/30">
                    <CardHeader>
                        <CardTitle className="text-base font-semibold flex items-center gap-2">
                            <Activity className="h-4 w-4 text-primary" />
                            SDK Traffic Volume
                        </CardTitle>
                        <CardDescription>Hourly evaluations processed across all environments (Last 24h)</CardDescription>
                    </CardHeader>
                    <CardContent className="px-2 sm:px-6">
                        <div className="h-[350px] w-full mt-4">
                            {totalEvaluations === 0 ? (
                                <div className="h-full w-full flex flex-col items-center justify-center text-muted-foreground bg-zinc-950/20 rounded-xl border border-dashed border-border/40">
                                    <BarChart2 className="h-10 w-10 text-muted-foreground/30 mb-3" />
                                    <p className="text-sm font-medium text-zinc-300">No Evaluation Data</p>
                                    <p className="text-xs text-zinc-500 max-w-[250px] text-center mt-1">There have been no SDK flag evaluations in the last 24 hours.</p>
                                </div>
                            ) : (
                                <ResponsiveContainer width="100%" height="100%" minHeight={1}>
                                    <AreaChart data={chartData} margin={CHART_MARGIN}>
                                    <defs>
                                        <linearGradient id="colorCount" x1="0" y1="0" x2="0" y2="1">
                                            <stop offset="5%" stopColor="#2563eb" stopOpacity={0.3} />
                                            <stop offset="95%" stopColor="#2563eb" stopOpacity={0} />
                                        </linearGradient>
                                    </defs>
                                    <CartesianGrid strokeDasharray="3 3" stroke="#3f3f46" vertical={false} opacity={0.4} />
                                    <XAxis
                                        dataKey="time"
                                        stroke="#a1a1aa"
                                        fontSize={12}
                                        tickLine={false}
                                        axisLine={false}
                                        tickMargin={10}
                                        minTickGap={30}
                                    />
                                    <YAxis
                                        stroke="#a1a1aa"
                                        fontSize={12}
                                        tickLine={false}
                                        axisLine={false}
                                        tickFormatter={(value) => value >= 1000 ? `${(value / 1000).toFixed(1)}k` : value}
                                        width={40}
                                    />
                                    <Tooltip
                                        contentStyle={{ backgroundColor: '#09090b', borderColor: '#27272a', borderRadius: '8px' }}
                                        itemStyle={{ color: '#e4e4e7' }}
                                        formatter={(value: any) => [value.toLocaleString(), 'Evaluations']}
                                        labelFormatter={(label) => `Time: ${label}`}
                                    />
                                    <Area
                                        type="monotone"
                                        dataKey="count"
                                        stroke="#3b82f6"
                                        strokeWidth={2}
                                        fillOpacity={1}
                                        fill="url(#colorCount)"
                                        activeDot={{ r: 4, strokeWidth: 0, fill: '#60a5fa' }}
                                    />
                                </AreaChart>
                            </ResponsiveContainer>
                            )}
                        </div>
                    </CardContent>
                </Card>

                <Card className="border-border/40 bg-zinc-950/30 flex flex-col">
                    <CardHeader className="flex flex-row items-start justify-between pb-4">
                        <div>
                            <CardTitle className="text-base font-semibold flex items-center gap-2">
                                <TrendingUp className="h-4 w-4 text-emerald-500" />
                                Experiment Insights
                            </CardTitle>
                            <CardDescription>Most significant A/B test results</CardDescription>
                        </div>
                        {dashboard.recentExperiments.length > 0 && (
                            <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-foreground shrink-0" onClick={() => navigate(`/projects/${projectId}/experiments`)}>
                                <ArrowRight className="h-4 w-4" />
                            </Button>
                        )}
                    </CardHeader>
                    <CardContent className="flex-1 flex flex-col gap-4">
                        {dashboard.recentExperiments.length === 0 ? (
                            <div className="flex-1 flex flex-col items-center justify-center text-center p-6 border border-dashed border-border/40 rounded-xl bg-zinc-900/20">
                                <TrendingUp className="h-8 w-8 text-muted-foreground/50 mb-3" />
                                <p className="text-sm font-medium">No Insights Yet</p>
                                <p className="text-xs text-muted-foreground mt-1 max-w-[200px]">
                                    Enable A/B testing on your flags to automatically detect winning features.
                                </p>
                                <Button variant="outline" size="sm" className="mt-4 text-xs" onClick={() => navigate(`/projects/${projectId}/flags`)}>
                                    Go to Feature Flags
                                </Button>
                            </div>
                        ) : (
                            <div className="space-y-3">
                                {dashboard.recentExperiments.slice(0, 3).map((exp, i) => {
                                    const isPositive = exp.expectedUplift > 0;
                                    const prob = Math.round(exp.probabilityToBeatBaseline * 100);
                                    const isSignificant = prob >= 95 || prob <= 5;

                                    return (
                                        <div key={i} className="p-3 rounded-lg border border-border/40 bg-zinc-900/40 hover:bg-zinc-900/80 transition-colors cursor-pointer" onClick={() => navigate(`/projects/${projectId}/flags/${exp.flagKey}?envId=${exp.environmentId}&tab=experiments&track=${exp.eventName}`)}>
                                            <div className="flex justify-between items-start mb-2">
                                                <div>
                                                    <div className="font-mono text-sm font-semibold text-zinc-200">{exp.flagKey}</div>
                                                    <div className="text-[10px] text-muted-foreground uppercase tracking-wider mt-0.5">{exp.environmentName} • {exp.eventName}</div>
                                                </div>
                                                {isSignificant && (
                                                    <Badge variant="outline" className={`text-[10px] ${isPositive ? 'border-emerald-500/30 text-emerald-500 bg-emerald-500/10' : 'border-rose-500/30 text-rose-500 bg-rose-500/10'}`}>
                                                        {isPositive ? 'Winner' : 'Anomaly'}
                                                    </Badge>
                                                )}
                                            </div>

                                            <div className="flex items-end justify-between mt-3">
                                                <div className="space-y-1">
                                                    <div className="text-[10px] text-muted-foreground">Expected Uplift</div>
                                                    <div className={`text-lg font-bold font-mono ${isPositive ? 'text-emerald-500' : 'text-rose-500'}`}>
                                                        {isPositive ? '+' : ''}{Math.round(exp.expectedUplift * 100)}%
                                                    </div>
                                                </div>
                                                <div className="text-right space-y-1">
                                                    <div className="text-[10px] text-muted-foreground">Confidence</div>
                                                    <div className="text-sm font-mono text-zinc-300">
                                                        {prob}%
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    );
                                })}
                            </div>
                        )}
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}
