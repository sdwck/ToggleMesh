import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Activity, BarChart2 } from 'lucide-react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend } from 'recharts';
import { format } from 'date-fns';
import { useExperimentTimeSeries } from '@/api/queries';

const COLORS = ['#888888', '#10b981', '#3b82f6', '#f59e0b', '#ec4899', '#8b5cf6', '#14b8a6'];

function getVariationName(id: string, index: number, variationsConfig?: any[]): string {
    const v = variationsConfig?.find(v => v.id === id);
    if (v && v.value) return v.value;
    return `Variation ${index + 1}`;
}

export function TimeSeriesChart({ projectId, envId, flagKey, eventName, variationsConfig }: { projectId: string, envId: string, flagKey: string, eventName: string, variationsConfig?: any[] }) {
    const { data: timeseries, isLoading } = useExperimentTimeSeries(projectId, envId, flagKey, eventName, 24);

    if (isLoading) return <div className="h-16 flex items-center justify-center text-sm text-muted-foreground">Loading time series...</div>;

    const uniqueVariationIds = Array.from(new Set((timeseries || []).map(pt => pt.variationId)));

    const grouped = (timeseries || []).reduce((acc, pt) => {
        if (!acc[pt.time]) acc[pt.time] = { time: pt.time };
        acc[pt.time][pt.variationId] = pt.conversionRate * 100;
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
                    {chartData.length === 0 ? (
                        <div className="h-full w-full flex flex-col items-center justify-center text-muted-foreground bg-zinc-950/20 rounded-xl border border-dashed border-border/40">
                            <BarChart2 className="h-10 w-10 text-muted-foreground/30 mb-3" />
                            <p className="text-sm font-medium text-zinc-300">No Time Series Data</p>
                            <p className="text-xs text-zinc-500 max-w-[250px] text-center mt-1">There is no conversion rate data available for this metric yet.</p>
                        </div>
                    ) : (
                        <>
                            <div className="print:hidden w-full h-full">
                                <ResponsiveContainer width="99%" height="100%" minWidth={0}>
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
                                            formatter={(val: any, name: any) => {
                                                const label = getVariationName(name, uniqueVariationIds.indexOf(name), variationsConfig);
                                                return typeof val === 'number' ? [`${val.toFixed(2)}%`, label] : [val, label];
                                            }}
                                        />
                                        <Legend verticalAlign="top" height={36} iconType="circle" formatter={(value) => {
                                            return getVariationName(value, uniqueVariationIds.indexOf(value), variationsConfig);
                                        }} />
                                        {uniqueVariationIds.map((vId, i) => (
                                            <Line key={vId} type="monotone" dataKey={vId} name={vId} stroke={COLORS[i % COLORS.length]} strokeWidth={3} dot={false} activeDot={{ r: 6 }} connectNulls={true} />
                                        ))}
                                    </LineChart>
                                </ResponsiveContainer>
                            </div>
                            <div className="hidden print:block w-[700px] h-[300px]">
                                <LineChart width={700} height={300} data={chartData} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
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
                                    {uniqueVariationIds.map((vId, i) => (
                                        <Line key={vId} type="monotone" dataKey={vId} name={vId} stroke={COLORS[i % COLORS.length]} strokeWidth={3} dot={false} activeDot={{ r: 6 }} connectNulls={true} />
                                    ))}
                                </LineChart>
                            </div>
                        </>
                    )}
                </div>
            </CardContent>
        </Card>
    );
}

export function SnapshotTimeSeriesChart({ data, variationsConfig }: { data: any[], variationsConfig?: any[] }) {
    if (!data || data.length === 0) return (
        <Card className="border-border/40 bg-zinc-950/50 mb-6 print:break-inside-avoid print:shadow-none print:border-zinc-300 print:bg-transparent">
            <CardHeader className="py-4 border-b border-border/40 bg-muted/20 print:border-zinc-300 print:bg-transparent">
                <CardTitle className="text-sm font-semibold flex items-center gap-2 print:text-black">
                    <Activity className="h-4 w-4 text-primary print:text-black" /> Conversion Rate Over Time (Historical)
                </CardTitle>
            </CardHeader>
            <CardContent className="p-6">
                <div className="h-[300px] w-full flex flex-col items-center justify-center text-muted-foreground bg-zinc-950/20 rounded-xl border border-dashed border-border/40">
                    <BarChart2 className="h-10 w-10 text-muted-foreground/30 mb-3" />
                    <p className="text-sm font-medium text-zinc-300">No Time Series Data</p>
                    <p className="text-xs text-zinc-500 max-w-[250px] text-center mt-1">There is no historical conversion rate data available.</p>
                </div>
            </CardContent>
        </Card>
    );

    const uniqueVariationIds = Array.from(new Set(data.map(pt => pt.variationId || pt.VariationId)));

    const grouped = data.reduce((acc, pt) => {
        const time = pt.time || pt.Time;
        if (!acc[time]) acc[time] = { time };
        const vId = pt.variationId || pt.VariationId;
        const cr = pt.conversionRate !== undefined ? pt.conversionRate : pt.ConversionRate;
        acc[time][vId] = cr * 100;
        return acc;
    }, {} as Record<string, any>);

    const chartData = Object.values(grouped).sort((a: any, b: any) => new Date(a.time).getTime() - new Date(b.time).getTime());

    return (
        <Card className="border-border/40 bg-zinc-950/50 mb-6 print:break-inside-avoid print:shadow-none print:border-zinc-300 print:bg-transparent">
            <CardHeader className="py-4 border-b border-border/40 bg-muted/20 print:border-zinc-300 print:bg-transparent">
                <CardTitle className="text-sm font-semibold flex items-center gap-2 print:text-black">
                    <Activity className="h-4 w-4 text-primary print:text-black" /> Conversion Rate Over Time (Historical)
                </CardTitle>
            </CardHeader>
            <CardContent className="p-6">
                <div className="h-[300px] w-full" style={{ minWidth: 0, minHeight: 0 }}>
                    <div className="print:hidden w-full h-full">
                        <ResponsiveContainer width="99%" height="100%" minWidth={0}>
                            <LineChart data={chartData} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                                <CartesianGrid strokeDasharray="3 3" stroke="#333" vertical={false} />
                                <XAxis
                                    dataKey="time"
                                    stroke="#888"
                                    fontSize={12}
                                    tickFormatter={(val) => format(new Date(val), 'MMM d, HH:mm')}
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
                                    formatter={(val: any, name: any) => {
                                        const label = getVariationName(name, uniqueVariationIds.indexOf(name), variationsConfig);
                                        return typeof val === 'number' ? [`${val.toFixed(2)}%`, label] : [val, label];
                                    }}
                                />
                                <Legend verticalAlign="top" height={36} iconType="circle" formatter={(value) => {
                                    return getVariationName(value, uniqueVariationIds.indexOf(value), variationsConfig);
                                }} />
                                {uniqueVariationIds.map((vId, i) => (
                                    <Line key={vId} type="monotone" dataKey={vId as string} name={vId as string} stroke={COLORS[i % COLORS.length]} strokeWidth={3} dot={false} activeDot={{ r: 6 }} />
                                ))}
                            </LineChart>
                        </ResponsiveContainer>
                    </div>
                    <div className="hidden print:block w-[700px] h-[300px]">
                        <LineChart width={700} height={300} data={chartData} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                            <CartesianGrid strokeDasharray="3 3" stroke="#333" vertical={false} />
                            <XAxis
                                dataKey="time"
                                stroke="#888"
                                fontSize={12}
                                tickFormatter={(val) => format(new Date(val), 'MMM d, HH:mm')}
                                tickMargin={10}
                            />
                            <YAxis
                                stroke="#888"
                                fontSize={12}
                                tickFormatter={(val) => typeof val === 'number' ? `${val.toFixed(1)}%` : val}
                                width={50}
                            />
                            {uniqueVariationIds.map((vId, i) => (
                                <Line key={vId} type="monotone" dataKey={vId as string} name={vId as string} stroke={COLORS[i % COLORS.length]} strokeWidth={3} dot={false} activeDot={{ r: 6 }} />
                            ))}
                        </LineChart>
                    </div>
                </div>
            </CardContent>
        </Card>
    );
}
