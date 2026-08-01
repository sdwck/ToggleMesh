import { useState, useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Clock, CheckCircle2, XCircle, AlertCircle, User, Code, Layers, Calendar, Filter, X } from 'lucide-react';
import { useInfinitePendingChanges, useReviewPendingChange, useProjectDetails, useSegments, useProjectFlags, useUserProfile, useCancelScheduledChange } from '@/api/queries';
import { ProjectRole } from '@/api/types';
import type { PendingChange } from '@/api/types';
import { toastApiError } from '@/api/errorUtils';
import { formatDistanceToNow, format } from 'date-fns';
import { toast } from 'sonner';
import { ChangeInstructionsRenderer } from './ChangeInstructionsRenderer';

export function PendingChangesTab({
    projectId,
    flagKey,
    environmentId
}: {
    projectId: string;
    flagKey: string;
    environmentId: string;
}) {
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
            dateFrom: dateFrom ? dateFrom : undefined,
            dateTo: dateTo ? dateTo : undefined,
            pageSize: 15,
            excludePurelyScheduled: true
        }
    );

    const changes = data?.pages.flatMap(p => p.items) || [];

    const { data: project } = useProjectDetails(projectId);
    const { data: segments } = useSegments(projectId, environmentId);
    const { data: flags } = useProjectFlags(projectId);
    const { data: user } = useUserProfile();
    const flag = flags?.find(f => f.key === flagKey);
    const envName = project?.environments?.find(e => e.id === environmentId)?.name || 'Environment';
    const reviewChange = useReviewPendingChange(projectId, flagKey, environmentId);
    const cancelChange = useCancelScheduledChange(projectId, flagKey, environmentId);

    const isOwnerOrAdmin = project?.userRole === ProjectRole.Owner || project?.userRole === ProjectRole.Admin;

    const handleCancel = async (changeId: string) => {
        try {
            await cancelChange.mutateAsync(changeId);
            toast.success('Scheduled change cancelled');
        } catch (err: any) {
            toastApiError(err, 'Failed to cancel scheduled change');
        }
    };

    const handleReview = async (changeId: string, action: 'Approve' | 'Reject') => {
        try {
            await reviewChange.mutateAsync({ changeId, action });
            toast.success(`Change request ${action.toLowerCase()}d successfully`);
        } catch (error: any) {
            toastApiError(error, `Failed to ${action.toLowerCase()} change`);
        }
    };

    const getStatusBadge = (status: PendingChange['status'], executeAt?: string | null) => {
        switch (status) {
            case 'PendingReview':
                return (
                    <div className="flex items-center gap-1.5">
                        <Badge variant="outline" className="border-amber-500/40 text-amber-400 bg-amber-500/10">
                            <Clock className="w-3 h-3 mr-1" /> Awaiting Review
                        </Badge>
                        {executeAt && (
                            <Badge variant="outline" className="border-blue-500/40 text-blue-400 bg-blue-500/10">
                                <Calendar className="w-3 h-3 mr-1" /> Scheduled
                            </Badge>
                        )}
                    </div>
                );
            case 'Scheduled':
                return executeAt ? (
                    <Badge variant="outline" className="border-blue-500/40 text-blue-400 bg-blue-500/10">
                        <Calendar className="w-3 h-3 mr-1" /> Scheduled
                    </Badge>
                ) : (
                    <Badge variant="outline" className="border-emerald-500/40 text-emerald-400 bg-emerald-500/10">
                        <CheckCircle2 className="w-3 h-3 mr-1" /> Approved
                    </Badge>
                );
            case 'Executed':
                return <Badge variant="outline" className="border-emerald-500/40 text-emerald-400 bg-emerald-500/10"><CheckCircle2 className="w-3 h-3 mr-1" /> Executed</Badge>;
            case 'Rejected':
                return <Badge variant="outline" className="border-destructive/40 text-destructive bg-destructive/10"><XCircle className="w-3 h-3 mr-1" /> Rejected</Badge>;
            case 'ConflictFailed':
                return <Badge variant="outline" className="border-destructive/40 text-destructive bg-destructive/10"><AlertCircle className="w-3 h-3 mr-1" /> Conflict Failed</Badge>;
            case 'Cancelled':
                return <Badge variant="outline" className="border-rose-500/40 text-rose-400 bg-rose-500/10"><XCircle className="w-3 h-3 mr-1" /> Cancelled</Badge>;
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

    const [headerActionsContainer, setHeaderActionsContainer] = useState<HTMLElement | null>(null);
    useEffect(() => {
        setHeaderActionsContainer(document.getElementById('pending-changes-header-actions'));
    }, []);

    const loadMoreRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const observer = new IntersectionObserver(
            (entries) => {
                if (entries[0].isIntersecting && hasNextPage && !isFetchingNextPage) {
                    fetchNextPage();
                }
            },
            { threshold: 0.1 }
        );

        if (loadMoreRef.current) {
            observer.observe(loadMoreRef.current);
        }

        return () => observer.disconnect();
    }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

    if (isLoading) {
        return <div className="text-xs text-muted-foreground p-4">Loading change history...</div>;
    }

    const headerActions = (
        <div className="flex items-center justify-end gap-3 w-full">
            <div className="flex items-center gap-2.5 ml-auto">
                <Badge variant="outline" className="border-purple-500/30 bg-purple-500/10 text-purple-300 font-mono text-xs gap-1.5 px-2.5 py-1 shadow-sm">
                    <Layers className="w-3.5 h-3.5 text-purple-400" /> {envName}
                </Badge>

                <Button
                    variant="ghost"
                    size="icon"
                    onClick={() => setIsFilterOpen(!isFilterOpen)}
                    title="Toggle date filter"
                    className={`h-7 w-7 text-zinc-400 hover:text-zinc-200 cursor-pointer ${isFilterOpen || dateFrom || dateTo ? 'bg-zinc-800 text-amber-400' : ''
                        }`}
                >
                    <Filter className="w-3.5 h-3.5" />
                </Button>
            </div>
        </div>
    );

    return (
        <div className="space-y-4">
            {headerActionsContainer ? createPortal(headerActions, headerActionsContainer) : headerActions}

            {isFilterOpen && (
                <div className="p-3.5 rounded-xl bg-zinc-900/80 backdrop-blur-md border border-zinc-800/80 shadow-xl space-y-3 transition-all animate-in fade-in slide-in-from-top-1 duration-200">
                    <div className="flex flex-wrap items-center justify-between gap-2 border-b border-zinc-800/60 pb-2">
                        <div className="flex items-center gap-1.5 text-xs text-zinc-300 font-medium">
                            <Calendar className="w-3.5 h-3.5 text-amber-400" /> Filter by Date Range
                        </div>
                        <div className="flex items-center gap-1">
                            <span className="text-[11px] text-zinc-500 mr-1 font-mono">Quick:</span>
                            <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setPreset(0)}
                                className="h-6 text-[11px] px-2 text-zinc-400 hover:text-zinc-100 hover:bg-zinc-800/80 cursor-pointer"
                            >
                                Today
                            </Button>
                            <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setPreset(7)}
                                className="h-6 text-[11px] px-2 text-zinc-400 hover:text-zinc-100 hover:bg-zinc-800/80 cursor-pointer"
                            >
                                7 Days
                            </Button>
                            <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setPreset(30)}
                                className="h-6 text-[11px] px-2 text-zinc-400 hover:text-zinc-100 hover:bg-zinc-800/80 cursor-pointer"
                            >
                                30 Days
                            </Button>
                        </div>
                    </div>
                    <div className="flex flex-wrap items-center gap-4">
                        <div className="flex items-center gap-2">
                            <span className="text-xs text-zinc-500 font-medium">From</span>
                            <Input
                                type="date"
                                value={dateFrom}
                                onClick={(e) => (e.target as HTMLInputElement).showPicker?.()}
                                onChange={(e) => setDateFrom(e.target.value)}
                                className="h-8 text-xs w-36 bg-zinc-950 border border-zinc-800 rounded-lg text-zinc-200 cursor-pointer focus-visible:ring-1 focus-visible:ring-amber-500/50 px-2.5 [color-scheme:dark]"
                            />
                        </div>
                        <div className="flex items-center gap-2">
                            <span className="text-xs text-zinc-500 font-medium">To</span>
                            <Input
                                type="date"
                                value={dateTo}
                                onClick={(e) => (e.target as HTMLInputElement).showPicker?.()}
                                onChange={(e) => setDateTo(e.target.value)}
                                className="h-8 text-xs w-36 bg-zinc-950 border border-zinc-800 rounded-lg text-zinc-200 cursor-pointer focus-visible:ring-1 focus-visible:ring-amber-500/50 px-2.5 [color-scheme:dark]"
                            />
                        </div>

                        {(dateFrom || dateTo) && (
                            <Button
                                variant="ghost"
                                size="icon"
                                onClick={() => { setDateFrom(''); setDateTo(''); }}
                                title="Clear Filters"
                                className="h-8 w-8 text-rose-400 hover:text-rose-300 hover:bg-rose-500/10 cursor-pointer ml-auto rounded-lg"
                            >
                                <X className="w-4 h-4" />
                            </Button>
                        )}
                    </div>
                </div>
            )}

            <div className="space-y-4">
                {changes.length === 0 ? (
                    <div className="text-center py-8 text-xs text-muted-foreground border border-dashed border-border/40 rounded-xl">
                        No change requests found for environment "{envName}".
                    </div>
                ) : (
                    changes.map((change) => (
                        <div key={change.id} className="p-4 rounded-xl border border-border/40 bg-zinc-900/30 space-y-3">
                            <div className="flex flex-wrap items-center justify-between gap-2">
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
                                    {getStatusBadge(change.status, change.executeAt)}
                                </div>
                            </div>

                            {change.comment && (
                                <p className="text-xs italic text-zinc-400 bg-zinc-950/50 p-2.5 rounded-lg border border-border/20">
                                    "{change.comment}"
                                </p>
                            )}

                            {change.executeAt && (
                                <div className="text-xs text-blue-400/90 bg-blue-500/10 px-3 py-1.5 rounded-lg flex items-center gap-1.5 border border-blue-500/20 font-mono">
                                    <Clock className="h-3.5 w-3.5" />
                                    Scheduled Execution: {format(new Date(change.executeAt), 'PPpp')}
                                </div>
                            )}

                            <ChangeInstructionsRenderer
                                jsonString={change.patchInstructionsJson}
                                diffSummaryJson={change.diffSummaryJson}
                                variations={flag?.variations}
                                segments={segments}
                                showRaw={!!rawJsonOpenMap[change.id]}
                            />

                            {(change.status === 'PendingReview' || change.status === 'Scheduled') && (
                                <div className="flex flex-wrap items-center justify-between gap-2 pt-4 border-t border-border/20">
                                    {change.status === 'PendingReview' ? (
                                        <div className="text-xs text-muted-foreground font-mono">
                                            Approvals: {change.approvedByUserIds.length} recorded
                                        </div>
                                    ) : !change.executeAt ? (
                                        <div className="text-xs text-muted-foreground italic">
                                            Approved change
                                        </div>
                                    ) : (
                                        <div className="text-xs text-muted-foreground italic">
                                            Scheduled change
                                        </div>
                                    )}
                                    <div className="flex items-center gap-2">
                                        {(isOwnerOrAdmin && change.requestedByUserId?.toLowerCase() !== user?.id?.toLowerCase()) && change.status === 'PendingReview' && (() => {
                                            const hasApproved = !!(user?.id && change.approvedByUserIds.some(id => id.toLowerCase() === user.id.toLowerCase()));
                                            return (
                                                <>
                                                    <Button
                                                        size="sm"
                                                        variant="outline"
                                                        disabled={reviewChange.isPending}
                                                        onClick={() => handleReview(change.id, 'Reject')}
                                                        className="text-xs h-7 text-rose-400 hover:bg-rose-500/10 border-rose-500/30 cursor-pointer"
                                                    >
                                                        Reject
                                                    </Button>
                                                    {!hasApproved && (
                                                        <Button
                                                            size="sm"
                                                            disabled={reviewChange.isPending}
                                                            onClick={() => handleReview(change.id, 'Approve')}
                                                            className="text-xs h-7 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold cursor-pointer"
                                                        >
                                                            Approve Change
                                                        </Button>
                                                    )}
                                                </>
                                            );
                                        })()}
                                        {change.requestedByUserId?.toLowerCase() === user?.id?.toLowerCase() && change.status === 'PendingReview' && (
                                            <Button
                                                size="sm"
                                                variant="outline"
                                                disabled={cancelChange.isPending}
                                                onClick={() => handleCancel(change.id)}
                                                className="text-xs h-7 text-rose-400 hover:bg-rose-500/10 border-rose-500/30 cursor-pointer"
                                            >
                                                Cancel Request
                                            </Button>
                                        )}
                                        {change.status === 'Scheduled' && (isOwnerOrAdmin || change.requestedByUserId?.toLowerCase() === user?.id?.toLowerCase()) && (
                                            <Button
                                                size="sm"
                                                variant="outline"
                                                disabled={cancelChange.isPending}
                                                onClick={() => handleCancel(change.id)}
                                                className="text-xs h-7 text-rose-400 hover:bg-rose-500/10 border-rose-500/30 cursor-pointer"
                                            >
                                                {change.executeAt ? "Cancel Schedule" : "Cancel"}
                                            </Button>
                                        )}
                                        {(!isOwnerOrAdmin && change.requestedByUserId?.toLowerCase() !== user?.id?.toLowerCase()) && change.status === 'PendingReview' && (
                                            <span className="text-[11px] text-muted-foreground italic">
                                                Awaiting review...
                                            </span>
                                        )}
                                    </div>
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
                    <div ref={loadMoreRef} className="pt-2 text-center h-10 flex items-center justify-center">
                        {isFetchingNextPage && (
                            <span className="text-xs text-zinc-500 animate-pulse">Loading more changes...</span>
                        )}
                    </div>
                )}
            </div>
        </div>
    );
}
