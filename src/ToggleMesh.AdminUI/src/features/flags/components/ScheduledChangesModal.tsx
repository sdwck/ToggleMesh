import { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Clock, Play, Trash2, User, Calendar, CheckCircle2, XCircle, Code, Layers, Filter, X } from 'lucide-react';
import { useInfinitePendingChanges, useCancelScheduledChange, useExecuteScheduledChange, useSegments, useProjectFlags } from '@/api/queries';
import { toastApiError } from '@/api/errorUtils';
import { formatDistanceToNow, format } from 'date-fns';
import { toast } from 'sonner';
import { ChangeInstructionsRenderer } from './ChangeInstructionsRenderer';
import type { PendingChange } from '@/api/types';

interface ScheduledChangesModalProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    projectId: string;
    flagKey: string;
    environmentId: string;
    environmentName?: string;
}

export function ScheduledChangesModal({
    open,
    onOpenChange,
    projectId,
    flagKey,
    environmentId,
    environmentName
}: ScheduledChangesModalProps) {
    const [tab, setTab] = useState<'active' | 'history'>('active');
    const [rawJsonOpenMap, setRawJsonOpenMap] = useState<Record<string, boolean>>({});
    const [isFilterOpen, setIsFilterOpen] = useState(false);
    const [dateFrom, setDateFrom] = useState('');
    const [dateTo, setDateTo] = useState('');

    const toggleRawJson = (id: string) => {
        setRawJsonOpenMap(prev => ({ ...prev, [id]: !prev[id] }));
    };

    const { data, isLoading, fetchNextPage, hasNextPage, isFetchingNextPage } = useInfinitePendingChanges(
        projectId,
        flagKey,
        environmentId,
        {
            status: tab === 'active' ? 'active' : 'history',
            dateFrom: dateFrom ? dateFrom : undefined,
            dateTo: dateTo ? dateTo : undefined,
            pageSize: 15
        }
    );

    const changes = data?.pages.flatMap(p => p.items) || [];

    const { data: segments } = useSegments(projectId, environmentId);
    const { data: flags } = useProjectFlags(projectId);
    const flag = flags?.find(f => f.key === flagKey);

    const cancelChange = useCancelScheduledChange(projectId, flagKey, environmentId);
    const executeChange = useExecuteScheduledChange(projectId, flagKey, environmentId);

    const scheduledOnly = changes.filter(c => !!c.executeAt);
    const activeChanges = scheduledOnly.filter(c => c.status === 'Scheduled');
    const historyChanges = scheduledOnly.filter(c => c.status === 'Executed' || c.status === 'Cancelled' || c.status === 'Expired' || c.status === 'Rejected');
    const currentList = tab === 'active' ? activeChanges : historyChanges;

    const handleExecuteNow = async (changeId: string) => {
        try {
            await executeChange.mutateAsync(changeId);
            toast.success('Scheduled change executed successfully');
        } catch (err: any) {
            toastApiError(err, 'Failed to execute scheduled change');
        }
    };

    const handleCancel = async (changeId: string) => {
        try {
            await cancelChange.mutateAsync(changeId);
            toast.success('Scheduled change cancelled');
        } catch (err: any) {
            toastApiError(err, 'Failed to cancel scheduled change');
        }
    };

    const getStatusBadge = (status: PendingChange['status']) => {
        switch (status) {
            case 'PendingReview':
                return <Badge variant="outline" className="border-amber-500/40 text-amber-400 bg-amber-500/10 p-1.5 cursor-help" title="Awaiting Review"><Clock className="w-3.5 h-3.5" /></Badge>;
            case 'Scheduled':
                return <Badge variant="outline" className="border-blue-500/40 text-blue-400 bg-blue-500/10"><Clock className="w-3 h-3 mr-1" /> Scheduled</Badge>;
            case 'Executed':
                return <Badge variant="outline" className="border-emerald-500/40 text-emerald-400 bg-emerald-500/10"><CheckCircle2 className="w-3 h-3 mr-1" /> Executed</Badge>;
            case 'Cancelled':
                return <Badge variant="outline" className="border-rose-500/40 text-rose-400 bg-rose-500/10"><XCircle className="w-3 h-3 mr-1" /> Cancelled</Badge>;
            case 'Rejected':
                return <Badge variant="outline" className="border-destructive/40 text-destructive bg-destructive/10"><XCircle className="w-3 h-3 mr-1" /> Rejected</Badge>;
            case 'Expired':
                return <Badge variant="outline" className="border-zinc-700 text-zinc-500"><Clock className="w-3 h-3 mr-1" /> Expired</Badge>;
            default:
                return <Badge variant="outline">{status}</Badge>;
        }
    };

    const setPreset = (days: number) => {
        const to = new Date();
        const from = new Date();
        if (days > 0) {
            from.setDate(from.getDate() - days);
        }
        setDateTo(to.toISOString().split('T')[0]);
        setDateFrom(from.toISOString().split('T')[0]);
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="w-full sm:max-w-2xl bg-zinc-950 p-0 border-zinc-800 max-h-[85vh] overflow-hidden flex flex-col">
                <DialogHeader className="p-6 pb-4 shrink-0 border-b border-zinc-800 bg-zinc-900/40 space-y-3">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                        <DialogTitle className="text-lg font-bold flex items-center gap-2">
                            <Clock className="h-5 w-5 text-blue-400" /> Scheduled Changes
                        </DialogTitle>

                        <div className="flex items-center gap-2 ml-auto mr-6">
                            {environmentName && (
                                <Badge variant="outline" className="border-purple-500/30 bg-purple-500/10 text-purple-300 font-mono text-xs gap-1.5 px-2.5 py-1 shadow-sm">
                                    <Layers className="w-3.5 h-3.5 text-purple-400" /> {environmentName}
                                </Badge>
                            )}

                            <Button
                                variant="ghost"
                                size="icon"
                                onClick={() => setIsFilterOpen(!isFilterOpen)}
                                title="Toggle date filter"
                                className={`h-7 w-7 text-zinc-400 hover:text-zinc-200 cursor-pointer ${
                                    isFilterOpen || dateFrom || dateTo ? 'bg-zinc-800 text-amber-400' : ''
                                }`}
                            >
                                <Filter className="w-3.5 h-3.5" />
                            </Button>
                        </div>
                    </div>

                    {isFilterOpen && (
                        <div className="p-3 rounded-lg bg-zinc-900/90 backdrop-blur-md border border-zinc-800 shadow-xl space-y-2.5 animate-in fade-in slide-in-from-top-1 duration-200">
                            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-zinc-800/80 pb-2">
                                <div className="flex items-center gap-1.5 text-xs text-zinc-300 font-medium">
                                    <Calendar className="w-3.5 h-3.5 text-amber-400" /> Filter by Date Range
                                </div>
                                <div className="flex items-center gap-1">
                                    <span className="text-[11px] text-zinc-500 mr-1 font-mono">Presets:</span>
                                    <Button
                                        variant="outline"
                                        size="sm"
                                        onClick={() => setPreset(0)}
                                        className="h-6 text-[11px] px-2 bg-zinc-950/60 border-zinc-800 text-zinc-300 hover:text-white hover:bg-zinc-800 cursor-pointer"
                                    >
                                        Today
                                    </Button>
                                    <Button
                                        variant="outline"
                                        size="sm"
                                        onClick={() => setPreset(7)}
                                        className="h-6 text-[11px] px-2 bg-zinc-950/60 border-zinc-800 text-zinc-300 hover:text-white hover:bg-zinc-800 cursor-pointer"
                                    >
                                        Last 7d
                                    </Button>
                                    <Button
                                        variant="outline"
                                        size="sm"
                                        onClick={() => setPreset(30)}
                                        className="h-6 text-[11px] px-2 bg-zinc-950/60 border-zinc-800 text-zinc-300 hover:text-white hover:bg-zinc-800 cursor-pointer"
                                    >
                                        Last 30d
                                    </Button>
                                </div>
                            </div>

                            <div className="flex flex-wrap items-center gap-3 pt-0.5">
                                <div className="flex items-center gap-2">
                                    <span className="text-zinc-400 text-xs font-medium">From</span>
                                    <Input
                                        type="date"
                                        value={dateFrom}
                                        onClick={(e) => (e.target as HTMLInputElement).showPicker?.()}
                                        onChange={(e) => setDateFrom(e.target.value)}
                                        className="h-8 text-xs w-36 bg-zinc-950 border-zinc-800 text-zinc-100 cursor-pointer focus-visible:ring-1 focus-visible:ring-amber-500/50 [color-scheme:dark]"
                                    />
                                </div>

                                <div className="flex items-center gap-2">
                                    <span className="text-zinc-400 text-xs font-medium">To</span>
                                    <Input
                                        type="date"
                                        value={dateTo}
                                        onClick={(e) => (e.target as HTMLInputElement).showPicker?.()}
                                        onChange={(e) => setDateTo(e.target.value)}
                                        className="h-8 text-xs w-36 bg-zinc-950 border-zinc-800 text-zinc-100 cursor-pointer focus-visible:ring-1 focus-visible:ring-amber-500/50 [color-scheme:dark]"
                                    />
                                </div>

                                {(dateFrom || dateTo) && (
                                    <Button
                                        variant="ghost"
                                        size="sm"
                                        onClick={() => { setDateFrom(''); setDateTo(''); }}
                                        className="h-8 text-xs text-rose-400 hover:text-rose-300 hover:bg-rose-500/10 gap-1.5 px-3 ml-auto cursor-pointer border border-rose-500/20"
                                    >
                                        <X className="w-3.5 h-3.5" /> Clear
                                    </Button>
                                )}
                            </div>
                        </div>
                    )}

                    <DialogDescription className="text-xs text-zinc-400">
                        View active rollouts and historical change requests for <span className="font-semibold text-zinc-200">{flagKey}</span> ({environmentName || 'Environment'}).
                    </DialogDescription>

                    <Tabs value={tab} onValueChange={(v) => setTab(v as any)} className="w-full pt-1">
                        <TabsList className="grid grid-cols-2 bg-zinc-900 border border-zinc-800">
                            <TabsTrigger value="active" className="text-xs data-[state=active]:bg-zinc-800">
                                Active Rollouts
                            </TabsTrigger>
                            <TabsTrigger value="history" className="text-xs data-[state=active]:bg-zinc-800">
                                Past History
                            </TabsTrigger>
                        </TabsList>
                    </Tabs>
                </DialogHeader>

                <div className="flex-1 overflow-y-auto p-6 space-y-4 bg-zinc-950/50">
                    {isLoading ? (
                        <div className="text-xs text-zinc-500 text-center py-8">Loading scheduled changes...</div>
                    ) : currentList.length === 0 ? (
                        <div className="text-center py-12 text-xs text-muted-foreground border border-dashed border-border/40 rounded-lg">
                            <Clock className="w-8 h-8 mx-auto mb-2 text-zinc-600 opacity-50" />
                            No {tab === 'active' ? 'active scheduled changes pending' : 'past scheduled changes history'} found for environment "{environmentName || 'Environment'}".
                        </div>
                    ) : (
                        currentList.map((change) => (
                            <div key={change.id} className="p-4 rounded-lg border border-border/40 bg-zinc-900/30 space-y-3">
                                <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border/20 pb-2.5">
                                    <div className="flex items-center gap-2 text-xs text-zinc-300">
                                        <User className="h-3.5 w-3.5 text-muted-foreground" />
                                        <span className="font-semibold cursor-help" title={change.requestedByUserEmail || change.requestedByUserName}>
                                            {change.requestedByUserName}
                                        </span>
                                        <span className="text-muted-foreground">• {formatDistanceToNow(new Date(change.createdAt), { addSuffix: true })}</span>
                                    </div>
                                    <div className="flex items-center gap-1 shrink-0">
                                        <Button
                                            variant="ghost"
                                            size="icon"
                                            onClick={() => toggleRawJson(change.id)}
                                            title={rawJsonOpenMap[change.id] ? "Hide Raw JSON" : "View Raw JSON"}
                                            className="h-6 w-6 text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50 cursor-pointer"
                                        >
                                            <Code className="w-3.5 h-3.5" />
                                        </Button>
                                        {getStatusBadge(change.status)}
                                    </div>
                                </div>

                                {change.executeAt && (
                                    <div className="text-xs text-blue-300 bg-blue-500/10 px-3 py-2 rounded flex items-center gap-2 border border-blue-500/20 font-medium">
                                        <Calendar className="h-4 w-4 text-blue-400" />
                                        Scheduled for: <span className="font-mono text-white">{format(new Date(change.executeAt), 'PPpp')}</span>
                                    </div>
                                )}

                                {change.comment && (
                                    <p className="text-xs italic text-zinc-400 bg-zinc-950/50 p-2.5 rounded border border-border/20">
                                        "{change.comment}"
                                    </p>
                                )}

                                <ChangeInstructionsRenderer
                                    jsonString={change.patchInstructionsJson}
                                    diffSummaryJson={change.diffSummaryJson}
                                    variations={flag?.variations}
                                    segments={segments}
                                    showRaw={!!rawJsonOpenMap[change.id]}
                                />

                                {tab === 'active' && (
                                    <div className="flex items-center justify-end gap-2 pt-3 border-t border-border/20">
                                        <Button
                                            size="sm"
                                            variant="outline"
                                            disabled={cancelChange.isPending || executeChange.isPending}
                                            onClick={() => handleCancel(change.id)}
                                            className="text-xs h-8 text-rose-400 hover:bg-rose-500/10 border-rose-500/30 cursor-pointer"
                                        >
                                            <Trash2 className="w-3.5 h-3.5 mr-1.5" /> Cancel Schedule
                                        </Button>
                                        {change.status === 'Scheduled' && (
                                            <Button
                                                size="sm"
                                                disabled={cancelChange.isPending || executeChange.isPending}
                                                onClick={() => handleExecuteNow(change.id)}
                                                className="text-xs h-8 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold cursor-pointer"
                                            >
                                                <Play className="w-3.5 h-3.5 mr-1.5 fill-current" /> Execute Immediately
                                            </Button>
                                        )}
                                    </div>
                                )}

                                {(change.status === 'Cancelled' || change.status === 'Rejected') && (change.reviewedByUserName || change.reviewedByUserEmail) && (
                                    <div className="pt-3 border-t border-border/20 text-xs text-rose-400/90 flex items-center justify-between">
                                        <span>
                                            {change.status === 'Cancelled' ? 'Cancelled by ' : 'Rejected by '}
                                            <span className="font-semibold cursor-help underline decoration-dotted" title={change.reviewedByUserEmail || change.reviewedByUserName}>
                                                {change.reviewedByUserName || change.reviewedByUserEmail}
                                            </span>
                                        </span>
                                    </div>
                                )}
                            </div>
                        ))
                    )}

                    {hasNextPage && (
                        <div className="pt-2 text-center">
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => fetchNextPage()}
                                disabled={isFetchingNextPage}
                                className="text-xs text-zinc-300 border-zinc-800 hover:bg-zinc-800/60 cursor-pointer"
                            >
                                {isFetchingNextPage ? 'Loading more...' : 'Load More Changes'}
                            </Button>
                        </div>
                    )}
                </div>
            </DialogContent>
        </Dialog>
    );
}
