import { useState, useEffect, useRef } from 'react';
import api from '@/api/axios';
import { formatDate } from '@/utils/dateFormatter';
import { useAuditLogs, useProjectAuditLogs } from '@/api/queries';
import { Card } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Input } from "@/components/ui/input";
import { toast } from 'sonner';
import { ClipboardList, Clock, Calendar, ChevronDown, Check, Download } from "lucide-react";
import { EmptyState } from "@/components/EmptyState";
import type { AuditLog } from '@/api/types';


const relativeRanges = [
    { label: 'Last 5 minutes', value: '5m' },
    { label: 'Last 15 minutes', value: '15m' },
    { label: 'Last 30 minutes', value: '30m' },
    { label: 'Last 1 hour', value: '1h' },
    { label: 'Last 3 hours', value: '3h' },
    { label: 'Last 6 hours', value: '6h' },
    { label: 'Last 12 hours', value: '12h' },
    { label: 'Last 24 hours', value: '24h' },
    { label: 'Last 2 days', value: '2d' },
    { label: 'Last 7 days', value: '7d' },
];

export interface AuditLogViewerProps {
    projectId?: string;
    environmentId?: string;
    pageSize?: number;
    hideExport?: boolean;
    className?: string;
    tableContainerClassName?: string;
}

export function AuditLogViewer({ 
    projectId, 
    environmentId, 
    pageSize = 10, 
    hideExport = false,
    className = "space-y-4 pb-2 h-full flex flex-col",
    tableContainerClassName = "overflow-auto flex-1 min-h-0"
}: AuditLogViewerProps) {
    const [filterAction, setFilterAction] = useState<string>('all');
    const [filterEntity, setFilterEntity] = useState<string>('all');
    const [sortOrder, setSortOrder] = useState<string>('desc');
    const [search, setSearch] = useState<string>('');
    const [debouncedSearch, setDebouncedSearch] = useState<string>('');
    const [rangeType, setRangeType] = useState<string>('all');
    const [customFrom, setCustomFrom] = useState<string>('');
    const [customTo, setCustomTo] = useState<string>('');
    const [isTimePickerOpen, setIsTimePickerOpen] = useState(false);
    const [isExporting, setIsExporting] = useState(false);
    const [selectedAuditLog, setSelectedAuditLog] = useState<AuditLog | null>(null);

    const timePickerRef = useRef<HTMLDivElement>(null);
    const loadMoreRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const timer = setTimeout(() => setDebouncedSearch(search), 300);
        return () => clearTimeout(timer);
    }, [search]);

    const fetchArgs = [
        pageSize, filterAction, filterEntity, sortOrder, rangeType, customFrom || undefined, customTo || undefined, debouncedSearch
    ] as const;


    const envData = useAuditLogs(environmentId || '', ...fetchArgs);
    const projData = useProjectAuditLogs(projectId || '', ...fetchArgs);

    const isEnvMode = !!environmentId;
    const { data: auditData, isLoading: isLoadingAudit, fetchNextPage, hasNextPage, isFetchingNextPage } = isEnvMode ? envData : projData;

    const items = auditData?.pages.flatMap(p => p.items) || [];

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

    useEffect(() => {
        function handleClickOutside(event: MouseEvent) {
            if (timePickerRef.current && !timePickerRef.current.contains(event.target as Node)) {
                setIsTimePickerOpen(false);
            }
        }
        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    const handleQuickRangeSelect = (val: string) => {
        setRangeType(val);
        setIsTimePickerOpen(false);
    };

    const handleApplyCustomRange = () => {
        setRangeType('custom');
        setIsTimePickerOpen(false);
    };

    const handleExportCsv = async () => {
        try {
            setIsExporting(true);
            const params = new URLSearchParams();
            if (isEnvMode && environmentId) params.append('environmentId', environmentId);
            else if (projectId) params.append('projectId', projectId);
            
            if (filterAction && filterAction !== 'all') params.append('action', filterAction);
            if (filterEntity && filterEntity !== 'all') params.append('entityName', filterEntity);
            if (sortOrder) params.append('sortOrder', sortOrder);
            if (debouncedSearch) params.append('search', debouncedSearch);

            if (rangeType === 'custom') {
                if (customFrom) params.append('dateFrom', new Date(customFrom).toISOString());
                if (customTo) params.append('dateTo', new Date(customTo).toISOString());
            } else if (rangeType !== 'all') {
                const now = new Date();
                let fromDate = new Date();
                const match = rangeType.match(/^(\d+)([mhd])$/);
                if (match) {
                    const val = parseInt(match[1]);
                    const unit = match[2];
                    if (unit === 'm') fromDate.setMinutes(now.getMinutes() - val);
                    else if (unit === 'h') fromDate.setHours(now.getHours() - val);
                    else if (unit === 'd') fromDate.setDate(now.getDate() - val);
                }
                params.append('dateFrom', fromDate.toISOString());
            }

            const response = await api.get(`/audit-logs/export?${params.toString()}`, {
                responseType: 'blob'
            });

            const blob = new Blob([response.data], { type: 'text/csv' });
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `ToggleMesh_${isEnvMode ? 'Env' : ''}AuditLogs_${isEnvMode ? environmentId : projectId}_${Date.now()}.csv`;
            document.body.appendChild(a);
            a.click();
            window.URL.revokeObjectURL(url);
            document.body.removeChild(a);
        } catch (error) {
            console.error('Failed to export CSV', error);
            toast.error('Failed to export Audit Logs');
        } finally {
            setIsExporting(false);
        }
    };

    const getTimeButtonLabel = () => {
        if (rangeType === 'all') return 'All Time';
        if (rangeType === 'custom') {
            const fromStr = customFrom ? formatDate(customFrom).split(' ')[0] : '...';
            const toStr = customTo ? formatDate(customTo).split(' ')[0] : 'now';
            return `${fromStr} to ${toStr}`;
        }
        return relativeRanges.find(r => r.value === rangeType)?.label || 'Select range';
    };

    return (
        <div className={className}>
            <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 pb-2 border-b border-border/10 w-full">
                <div className="flex gap-3 flex-wrap items-center flex-1">
                    <Input
                        type="search"
                        placeholder="Search logs..."
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                        className="w-[200px] h-10 bg-zinc-950/20 border-border/40 text-xs"
                    />
                    <Select value={filterAction} onValueChange={setFilterAction}>
                        <SelectTrigger className="w-[140px] h-10 bg-zinc-950/20 border-border/40 text-xs">
                            <SelectValue placeholder="Action" />
                        </SelectTrigger>
                        <SelectContent className="border-border/40 bg-zinc-950">
                            <SelectItem value="all">All Actions</SelectItem>
                            <SelectItem value="added">Added</SelectItem>
                            <SelectItem value="modified">Modified</SelectItem>
                            <SelectItem value="deleted">Deleted</SelectItem>
                        </SelectContent>
                    </Select>

                    <Select value={filterEntity} onValueChange={setFilterEntity}>
                        <SelectTrigger className="w-[140px] h-10 bg-zinc-950/20 border-border/40 text-xs">
                            <SelectValue placeholder="Entity" />
                        </SelectTrigger>
                        <SelectContent className="border-border/40 bg-zinc-950">
                            <SelectItem value="all">All Entities</SelectItem>
                            {isEnvMode ? (
                                <>
                                    <SelectItem value="flagenvironmentstate">Flags Status</SelectItem>
                                    <SelectItem value="flagrule">Rules</SelectItem>
                                    <SelectItem value="environmentkey">API Keys</SelectItem>
                                </>
                            ) : (
                                <>
                                    <SelectItem value="featureflag">Feature Flags</SelectItem>
                                    <SelectItem value="projectenvironment">Environments</SelectItem>
                                    <SelectItem value="projectmember">Members</SelectItem>
                                    <SelectItem value="webhook">Webhooks</SelectItem>
                                    <SelectItem value="project">Project</SelectItem>
                                </>
                            )}
                        </SelectContent>
                    </Select>

                    <Select value={sortOrder} onValueChange={setSortOrder}>
                        <SelectTrigger className="w-[140px] h-10 bg-zinc-950/20 border-border/40 font-mono text-xs">
                            <SelectValue placeholder="Sort order" />
                        </SelectTrigger>
                        <SelectContent className="border-border/40 bg-zinc-950">
                            <SelectItem value="desc">Newest First</SelectItem>
                            <SelectItem value="asc">Oldest First</SelectItem>
                        </SelectContent>
                    </Select>

                    <div className="relative" ref={timePickerRef}>
                        <Button
                            variant="outline"
                            onClick={() => setIsTimePickerOpen(!isTimePickerOpen)}
                            className={`h-10 px-3 text-xs font-medium border-border/40 bg-zinc-950/20 flex items-center gap-2 cursor-pointer transition-all ${isTimePickerOpen ? 'border-primary/50 bg-muted/20' : ''}`}
                        >
                            <Clock className="h-3.5 w-3.5 text-muted-foreground" />
                            <span className="font-mono">{getTimeButtonLabel()}</span>
                            <ChevronDown className="h-3 w-3 text-muted-foreground ml-1" />
                        </Button>

                        {isTimePickerOpen && (
                            <div className="absolute top-11 left-0 sm:left-auto sm:right-0 bg-zinc-950 border border-border/40 rounded-lg p-1.5 shadow-[0_16px_40px_rgba(0,0,0,0.8)] z-[60] flex flex-col md:flex-row min-w-[280px] md:min-w-[520px] animate-in fade-in slide-in-from-top-2 duration-150">
                                <div className="flex-1 p-3 border-b md:border-b-0 md:border-r border-border/20 flex flex-col justify-between">
                                    <div className="space-y-4">
                                        <div className="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider flex items-center gap-2 h-4 leading-none">
                                            <Calendar className="h-3.5 w-3.5" />
                                            <span>Time Range</span>
                                        </div>
                                        <div className="space-y-3 py-1">
                                            <div className="space-y-1.5">
                                                <label className="text-[10px] text-muted-foreground font-medium uppercase leading-none block">From</label>
                                                <div className="relative">
                                                    <Input
                                                        type="datetime-local"
                                                        value={customFrom}
                                                        onChange={(e) => setCustomFrom(e.target.value)}
                                                        className="h-8 border-border/40 bg-zinc-900/50 font-mono text-[11px] pl-3 pr-10 cursor-pointer w-full [&::-webkit-calendar-picker-indicator]:opacity-0 [&::-webkit-calendar-picker-indicator]:absolute [&::-webkit-calendar-picker-indicator]:inset-0 [&::-webkit-calendar-picker-indicator]:w-full [&::-webkit-calendar-picker-indicator]:h-full [&::-webkit-calendar-picker-indicator]:cursor-pointer"
                                                    />
                                                    <Calendar className="absolute right-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
                                                </div>
                                            </div>
                                            <div className="space-y-1.5">
                                                <label className="text-[10px] text-muted-foreground font-medium uppercase leading-none block">To</label>
                                                <div className="relative">
                                                    <Input
                                                        type="datetime-local"
                                                        value={customTo}
                                                        onChange={(e) => setCustomTo(e.target.value)}
                                                        className="h-8 border-border/40 bg-zinc-900/50 font-mono text-[11px] pl-3 pr-10 cursor-pointer w-full [&::-webkit-calendar-picker-indicator]:opacity-0 [&::-webkit-calendar-picker-indicator]:absolute [&::-webkit-calendar-picker-indicator]:inset-0 [&::-webkit-calendar-picker-indicator]:w-full [&::-webkit-calendar-picker-indicator]:h-full [&::-webkit-calendar-picker-indicator]:cursor-pointer"
                                                    />
                                                    <Calendar className="absolute right-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                    <Button className="w-full h-8 text-xs font-semibold cursor-pointer mt-auto" onClick={handleApplyCustomRange}>
                                        Apply time range
                                    </Button>
                                </div>

                                <div className="w-full md:w-[220px] p-3 flex flex-col justify-between">
                                    <div className="space-y-3 flex flex-col h-full">
                                        <div className="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider flex items-center gap-2 h-4 leading-none px-1">
                                            <Clock className="h-3.5 w-3.5" />
                                            <span>Quick Ranges</span>
                                        </div>
                                        <div className="grid grid-cols-2 md:grid-cols-1 gap-0.5 max-h-[11rem] overflow-y-auto pr-1 flex-1">
                                            <button
                                                onClick={() => handleQuickRangeSelect('all')}
                                                className={`w-full text-left px-2 py-1.5 rounded-md text-xs font-medium transition-colors flex items-center justify-between cursor-pointer ${rangeType === 'all' ? 'bg-primary/10 text-primary' : 'text-zinc-400 hover:bg-muted/30 hover:text-zinc-200'}`}
                                            >
                                                <span>All Time</span>
                                                {rangeType === 'all' && <Check className="h-3 w-3" />}
                                            </button>
                                            {relativeRanges.map((range) => {
                                                const isSelected = rangeType === range.value;
                                                return (
                                                    <button
                                                        key={range.value}
                                                        onClick={() => handleQuickRangeSelect(range.value)}
                                                        className={`w-full text-left px-2 py-1.5 rounded-md text-xs font-medium transition-colors flex items-center justify-between cursor-pointer ${isSelected ? 'bg-primary/10 text-primary' : 'text-zinc-400 hover:bg-muted/30 hover:text-zinc-200'}`}
                                                    >
                                                        <span>{range.label}</span>
                                                        {isSelected && <Check className="h-3 w-3" />}
                                                    </button>
                                                );
                                            })}
                                        </div>
                                    </div>
                                </div>
                            </div>
                        )}
                    </div>
                </div>
                
                {!hideExport && (
                    <Button 
                        variant="outline" 
                        size="sm" 
                        className="h-10 px-3 bg-zinc-950/20 border-border/40 hover:bg-muted/20 whitespace-nowrap"
                        onClick={handleExportCsv}
                        disabled={isExporting}
                    >
                        <Download className="h-4 w-4 mr-2" />
                        {isExporting ? 'Exporting...' : 'Export CSV'}
                    </Button>
                )}
            </div>

            <Card className="border-border/40 bg-zinc-950/20 overflow-hidden shadow-lg flex flex-col h-full min-h-0">
                <div className={tableContainerClassName}>
                    <Table>
                        <TableHeader className="sticky top-0 bg-background z-10">
                            <TableRow className="hover:bg-transparent border-border/40 shadow-sm h-10">
                                <TableHead className="w-[180px]">Timestamp</TableHead>
                                <TableHead className="w-[120px]">Action</TableHead>
                                <TableHead>Entity</TableHead>
                                {!isEnvMode && <TableHead>Friendly Name</TableHead>}
                                <TableHead>Performed By</TableHead>
                                <TableHead className="text-right w-[80px]">Details</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {isLoadingAudit && items.length === 0 ? (
                                Array.from({ length: 3 }).map((_, i) => (
                                    <TableRow key={i} className="border-border/40 h-[40px]">
                                        <TableCell><Skeleton className="h-4 w-32 rounded font-mono" /></TableCell>
                                        <TableCell><Skeleton className="h-5 w-16 rounded" /></TableCell>
                                        <TableCell><Skeleton className="h-4 w-24 rounded font-mono" /></TableCell>
                                        {!isEnvMode && <TableCell><Skeleton className="h-4 w-40 rounded" /></TableCell>}
                                        <TableCell><Skeleton className="h-4 w-28 rounded" /></TableCell>
                                        <TableCell className="text-right"><Skeleton className="h-4 w-12 ml-auto rounded" /></TableCell>
                                    </TableRow>
                                ))
                            ) : items.length === 0 ? (
                                <TableRow className="hover:bg-transparent">
                                    <TableCell colSpan={isEnvMode ? 5 : 6} className="p-0 border-none h-24">
                                        <EmptyState
                                            icon={ClipboardList}
                                            title="No Audit Logs Found"
                                            description="We couldn't find any activities matching your filters."
                                        />
                                    </TableCell>
                                </TableRow>
                            ) : (
                                items.map((log) => (
                                    <TableRow key={log.id} className="border-border/40 hover:bg-muted/30 text-sm h-4">
                                        <TableCell className="text-muted-foreground py-[0.95rem] font-mono text-xs">
                                            {formatDate(log.timestamp)}
                                        </TableCell>
                                        <TableCell className="py-2">
                                            <Badge variant="outline" className="text-[10px] font-mono uppercase">
                                                {log.action}
                                            </Badge>
                                        </TableCell>
                                        <TableCell className="font-mono text-xs py-2 text-primary/80">
                                            {log.entityName} {isEnvMode && log.entityFriendlyName ? `(${log.entityFriendlyName})` : ''}
                                        </TableCell>
                                        {!isEnvMode && (
                                            <TableCell className="font-medium text-xs py-2">{log.entityFriendlyName || log.entityId}</TableCell>
                                        )}
                                        <TableCell className="text-muted-foreground py-2 font-mono text-xs">
                                            {log.performedByEmail || log.performedBy}
                                        </TableCell>
                                        <TableCell className="text-right py-2">
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                onClick={() => setSelectedAuditLog(log)}
                                                disabled={!log.oldValues && !log.newValues}
                                                className="h-6 cursor-pointer"
                                            >
                                                View
                                            </Button>
                                        </TableCell>
                                    </TableRow>
                                ))
                            )}
                            {hasNextPage && (
                                <TableRow>
                                    <TableCell colSpan={isEnvMode ? 5 : 6} className="h-14 text-center">
                                        <div ref={loadMoreRef} className="flex justify-center items-center h-full">
                                            <span className="text-sm text-muted-foreground animate-pulse">Loading more...</span>
                                        </div>
                                    </TableCell>
                                </TableRow>
                            )}
                        </TableBody>
                    </Table>
                </div>
            </Card>

            <Dialog open={!!selectedAuditLog} onOpenChange={(open) => !open && setSelectedAuditLog(null)}>
                <DialogContent className="max-w-3xl max-h-[80vh] overflow-y-auto border-border/40 bg-zinc-950 z-[110]">
                    <DialogHeader>
                        <DialogTitle>Audit Log Details</DialogTitle>
                        <DialogDescription>
                            {selectedAuditLog?.action} on {selectedAuditLog?.entityName} ({selectedAuditLog?.entityFriendlyName || selectedAuditLog?.entityId}) at {selectedAuditLog ? formatDate(selectedAuditLog.timestamp) : ''}
                        </DialogDescription>
                    </DialogHeader>

                    <div className="grid grid-cols-2 gap-4 mt-4">
                        <div className="space-y-2">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Previous State</h4>
                            <div className="bg-rose-950/10 border border-rose-500/10 p-4 rounded-md font-mono text-xs overflow-x-auto whitespace-pre-wrap text-rose-400">
                                {selectedAuditLog?.oldValues && selectedAuditLog.oldValues !== "{}" ? (
                                    <pre>{JSON.stringify(JSON.parse(selectedAuditLog.oldValues), null, 2)}</pre>
                                ) : (
                                    <span>None</span>
                                )}
                            </div>
                        </div>
                        <div className="space-y-2">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">New State</h4>
                            <div className="bg-emerald-950/10 border border-emerald-500/10 p-4 rounded-md font-mono text-xs overflow-x-auto whitespace-pre-wrap text-emerald-400">
                                {selectedAuditLog?.newValues && selectedAuditLog.newValues !== "{}" ? (
                                    <pre>{JSON.stringify(JSON.parse(selectedAuditLog.newValues), null, 2)}</pre>
                                ) : (
                                    <span>None</span>
                                )}
                            </div>
                        </div>
                    </div>

                    <DialogFooter className="mt-4">
                        <Button onClick={() => setSelectedAuditLog(null)}>Close</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
