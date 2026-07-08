import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Activity } from 'lucide-react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend } from 'recharts';
import { format } from 'date-fns';
import { useExperimentTimeSeries } from '@/api/queries';

export function TimeSeriesChart({ projectId, envId, flagKey, eventName }: { projectId: string, envId: string, flagKey: string, eventName: string }) {
    const { data: timeseries, isLoading } = useExperimentTimeSeries(projectId, envId, flagKey, eventName, 1);

    if (isLoading) return <div className="h-16 flex items-center justify-center text-sm text-muted-foreground">Loading time series...</div>;
    if (!timeseries || timeseries.length === 0) return <div className="h-16 flex items-center justify-center text-sm text-muted-foreground">No time series data available yet.</div>;

    const grouped = timeseries.reduce((acc, pt) => {
        if (!acc[pt.time]) acc[pt.time] = { time: pt.time };
        if (pt.variant) {
            acc[pt.time].TreatmentCR = pt.conversionRate * 100;
        } else {
            acc[pt.time].ControlCR = pt.conversionRate * 100;
        }
        return acc;
    }, {} as Record<string, any>);

    const chartData = Object.values(grouped).sort((a: any, b: any) => new Date(a.time).getTime() - new Date(b.time).getTime());

    return (
        <Card className="border-border/40 bg-zinc-950/50 mb-6 print:break-inside-avoid print:shadow-none print:border-zinc-300 print:bg-transparent">
            <CardHeader className="py-4 border-b border-border/40 bg-muted/20 print:border-zinc-300 print:bg-transparent">
                <CardTitle className="text-sm font-semibold flex items-center gap-2 print:text-black">
                    <Activity className="h-4 w-4 text-primary print:text-black" /> Conversion Rate Over Time (24h)
                </CardTitle>
            </CardHeader>
            <CardContent className="p-6">
                <div className="h-[300px] w-full" style={{ minWidth: 0, minHeight: 0 }}>
                    <ResponsiveContainer width="100%" height="100%" minWidth={0}>
                        <LineChart data={chartData} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                            <CartesianGrid strokeDasharray="3 3" stroke="#333" vertical={false} />
                            <XAxis
                                dataKey="time"
                                stroke="#888"
                                fontSize={12}
                                tickFormatter={(val) => format(new Date(val), 'HH:mm')}
                                tickMargin={10}
                            />
                            <YAxis
                                stroke="#888"
                                fontSize={12}
                                tickFormatter={(val) => typeof val === 'number' ? `${val.toFixed(1)}%` : val}
                                width={50}
                            />
                            <Tooltip
                                contentStyle={{ backgroundColor: '#18181b', borderColor: '#333', color: '#fff', borderRadius: '8px' }}
                                itemStyle={{ color: '#fff' }}
                                labelFormatter={(val) => format(new Date(val), 'MMM d, HH:mm')}
                                formatter={(val: any) => typeof val === 'number' ? [`${val.toFixed(2)}%`, undefined] : [val, undefined]}
                            />
                            <Legend verticalAlign="top" height={36} iconType="circle" />
                            <Line type="monotone" name="Control" dataKey="ControlCR" stroke="#888" strokeWidth={3} dot={false} activeDot={{ r: 6 }} />
                            <Line type="monotone" name="Treatment" dataKey="TreatmentCR" stroke="#10b981" strokeWidth={3} dot={false} activeDot={{ r: 6 }} />
                        </LineChart>
                    </ResponsiveContainer>
                </div>
            </CardContent>
        </Card>
    );
}

export function SnapshotTimeSeriesChart({ data }: { data: any[] }) {
    if (!data || data.length === 0) return (
        <Card className="border-border/40 bg-zinc-950/50 mb-6 print:break-inside-avoid print:shadow-none print:border-zinc-300 print:bg-transparent">
            <CardHeader className="py-4 border-b border-border/40 bg-muted/20 print:border-zinc-300 print:bg-transparent">
                <CardTitle className="text-sm font-semibold flex items-center gap-2 print:text-black">
                    <Activity className="h-4 w-4 text-primary print:text-black" />
                    Conversion Rate Over Time
                    <Badge variant="outline" className="ml-2 text-[10px] font-normal text-amber-500 border-amber-500/30 print:hidden">Historical</Badge>
                </CardTitle>
            </CardHeader>
            <CardContent className="p-6">
                <div className="h-16 flex items-center justify-center text-sm text-muted-foreground">
                    No time series data available for this historical snapshot.
                </div>
            </CardContent>
        </Card>
    );

    const grouped = data.reduce((acc, pt) => {
        if (!acc[pt.time]) acc[pt.time] = { time: pt.time };
        if (pt.variant) {
            acc[pt.time].TreatmentCR = pt.conversionRate * 100;
        } else {
            acc[pt.time].ControlCR = pt.conversionRate * 100;
        }
        return acc;
    }, {} as Record<string, any>);

    const chartData = Object.values(grouped).sort((a: any, b: any) => new Date(a.time).getTime() - new Date(b.time).getTime());

    if (chartData.length === 0) return null;

    return (
        <Card className="border-border/40 bg-zinc-950/50 mb-6 print:break-inside-avoid print:shadow-none print:border-zinc-300 print:bg-transparent">
            <CardHeader className="py-4 border-b border-border/40 bg-muted/20 print:border-zinc-300 print:bg-transparent">
                <CardTitle className="text-sm font-semibold flex items-center gap-2 print:text-black">
                    <Activity className="h-4 w-4 text-primary print:text-black" />
                    Conversion Rate Over Time
                    <Badge variant="outline" className="ml-2 text-[10px] font-normal text-amber-500 border-amber-500/30 print:hidden">Historical</Badge>
                </CardTitle>
            </CardHeader>
            <CardContent className="p-6">
                <div className="h-[300px] w-full" style={{ minWidth: 0, minHeight: 0 }}>
                    <ResponsiveContainer width="100%" height="100%" minWidth={0}>
                        <LineChart data={chartData} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                            <CartesianGrid strokeDasharray="3 3" stroke="#333" vertical={false} />
                            <XAxis
                                dataKey="time"
                                stroke="#888"
                                fontSize={12}
                                tickFormatter={(val) => format(new Date(val), 'HH:mm')}
                                tickMargin={10}
                            />
                            <YAxis
                                stroke="#888"
                                fontSize={12}
                                tickFormatter={(val) => typeof val === 'number' ? `${val.toFixed(1)}%` : val}
                                width={50}
                            />
                            <Tooltip
                                contentStyle={{ backgroundColor: '#18181b', borderColor: '#333', color: '#fff', borderRadius: '8px' }}
                                itemStyle={{ color: '#fff' }}
                                labelFormatter={(val) => format(new Date(val), 'MMM d, HH:mm')}
                                formatter={(val: any) => typeof val === 'number' ? [`${val.toFixed(2)}%`, undefined] : [val, undefined]}
                            />
                            <Legend verticalAlign="top" height={36} iconType="circle" />
                            <Line type="monotone" name="Control" dataKey="ControlCR" stroke="#888" strokeWidth={3} dot={false} activeDot={{ r: 6 }} />
                            <Line type="monotone" name="Treatment" dataKey="TreatmentCR" stroke="#10b981" strokeWidth={3} dot={false} activeDot={{ r: 6 }} />
                        </LineChart>
                    </ResponsiveContainer>
                </div>
            </CardContent>
        </Card>
    );
}
