import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Box, ArrowRightLeft, FileClock, Settings, Key, GripVertical, Clock, Calendar, ChevronDown, Check, Download } from 'lucide-react';
import { useCreateEnvironment, useCloneEnvironment, useAuditLogs, useReorderEnvironments } from '@/api/queries';
import api from '@/api/axios';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import {
    Form,
    FormControl,
    FormField,
    FormItem,
    FormMessage,
} from '@/components/ui/form';
import { handleApiError } from '@/api/errorUtils';

const createEnvSchema = z.object({
    name: z.string().min(1, 'Environment name is required')
});
type CreateEnvValues = z.infer<typeof createEnvSchema>;

const syncEnvSchema = z.object({
    sourceEnvId: z.string().min(1, 'Please select a source environment')
});
type SyncEnvValues = z.infer<typeof syncEnvSchema>;
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger
} from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ProjectRole, type AuditLog, type Environment, type ProjectDetails } from '@/api/types';
import { toast } from 'sonner';
import { Skeleton } from '@/components/ui/skeleton';

const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const pad = (num: number) => String(num).padStart(2, '0');

    const year = date.getFullYear();
    const month = pad(date.getMonth() + 1);
    const day = pad(date.getDate());
    const hours = pad(date.getHours());
    const minutes = pad(date.getMinutes());
    const seconds = pad(date.getSeconds());

    return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
};

const getEnvBadgeStyle = (name: string) => {
    const lower = name.toLowerCase();
    if (lower.includes('prod') || lower.includes('prd')) {
        return "bg-rose-500/10 text-rose-400 border-rose-500/20";
    }
    if (lower.includes('dev') || lower.includes('local')) {
        return "bg-emerald-500/10 text-emerald-400 border-emerald-500/20";
    }
    if (lower.includes('stg') || lower.includes('stage') || lower.includes('test') || lower.includes('qa')) {
        return "bg-amber-500/10 text-amber-400 border-amber-500/20";
    }
    return "bg-blue-500/10 text-blue-400 border-blue-500/20";
};

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

function EnvironmentAuditLogs({ envId }: { envId: string }) {
    const pageSize = 6;

    const [filterAction, setFilterAction] = useState<string>('all');
    const [filterEntity, setFilterEntity] = useState<string>('all');
    const [sortOrder, setSortOrder] = useState<string>('desc');
    const [search, setSearch] = useState<string>('');
    const [debouncedSearch, setDebouncedSearch] = useState<string>('');

    const [rangeType, setRangeType] = useState<string>('all');
    const [customFrom, setCustomFrom] = useState<string>('');
    const [customTo, setCustomTo] = useState<string>('');
    const [isExporting, setIsExporting] = useState(false);

    const [isTimePickerOpen, setIsTimePickerOpen] = useState(false);
    const timePickerRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const timer = setTimeout(() => setDebouncedSearch(search), 300);
        return () => clearTimeout(timer);
    }, [search]);

    const {
        data,
        isLoading,
        fetchNextPage,
        hasNextPage,
        isFetchingNextPage
    } = useAuditLogs(
        envId,
        pageSize,
        filterAction,
        filterEntity,
        sortOrder,
        rangeType,
        customFrom || undefined,
        customTo || undefined,
        debouncedSearch
    );

    const items = data?.pages.flatMap(p => p.items) || [];
    const [selectedLog, setSelectedLog] = useState<AuditLog | null>(null);

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
            if (envId) params.append('environmentId', envId);
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
            a.download = `ToggleMesh_EnvAuditLogs_${envId}_${Date.now()}.csv`;
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

    const handleActionChange = (val: string) => {
        setFilterAction(val);
    };

    const handleEntityChange = (val: string) => {
        setFilterEntity(val);
    };

    const handleSortChange = (val: string) => {
        setSortOrder(val);
    };

    return (
        <div className="h-full flex flex-col justify-between space-y-4">
            <div className="flex flex-col flex-1 min-h-0 space-y-4">
                <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 w-full">
                    <div className="flex gap-4 shrink-0 flex-wrap items-center flex-1">
                        <Input
                            type="search"
                            placeholder="Search logs..."
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                            className="w-[180px] h-10 bg-zinc-950/20 border-border/40 text-xs"
                        />
                        <Select value={filterAction} onValueChange={handleActionChange}>
                            <SelectTrigger className="w-[130px] h-10 bg-zinc-950/20 border-border/40 text-xs">
                                <SelectValue placeholder="Action" />
                            </SelectTrigger>
                            <SelectContent className="border-border/40 bg-zinc-950">
                                <SelectItem value="all">All Actions</SelectItem>
                                <SelectItem value="added">Added</SelectItem>
                                <SelectItem value="modified">Modified</SelectItem>
                                <SelectItem value="deleted">Deleted</SelectItem>
                            </SelectContent>
                        </Select>

                        <Select value={filterEntity} onValueChange={handleEntityChange}>
                            <SelectTrigger className="w-[130px] h-10 bg-zinc-950/20 border-border/40 text-xs">
                                <SelectValue placeholder="Entity" />
                            </SelectTrigger>
                            <SelectContent className="border-border/40 bg-zinc-950">
                                <SelectItem value="all">All Entities</SelectItem>
                                <SelectItem value="flagenvironmentstate">Flags Status</SelectItem>
                                <SelectItem value="flagrule">Rules</SelectItem>
                                <SelectItem value="environmentkey">API Keys</SelectItem>
                            </SelectContent>
                        </Select>

                        <Select value={sortOrder} onValueChange={handleSortChange}>
                            <SelectTrigger className="w-[130px] h-10 bg-zinc-950/20 border-border/40 text-xs font-mono">
                                <SelectValue placeholder="Sort" />
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
                                className={`h-10 px-3 text-xs font-medium border-border/40 bg-zinc-950/20 flex items-center gap-2 cursor-pointer transition-all ${isTimePickerOpen ? 'border-primary/50 bg-muted/20' : ''
                                    }`}
                            >
                                <Clock className="h-3.5 w-3.5 text-muted-foreground" />
                                <span className="font-mono">{getTimeButtonLabel()}</span>
                                <ChevronDown className="h-3 w-3 text-muted-foreground ml-1" />
                            </Button>

                            {isTimePickerOpen && (
                                <div className="absolute top-11 right-0 bg-zinc-950 border border-border/40 rounded-lg p-1.5 shadow-[0_16px_40px_rgba(0,0,0,0.8)] z-[60] flex flex-col md:flex-row min-w-[280px] md:min-w-[520px] animate-in fade-in slide-in-from-top-2 duration-150">
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
                                                            className="h-8 border-border/40 bg-zinc-900/50 font-mono text-[11px] pl-3 pr-10 cursor-pointer w-full
                                                    [&::-webkit-calendar-picker-indicator]:opacity-0 [&::-webkit-calendar-picker-indicator]:absolute [&::-webkit-calendar-picker-indicator]:inset-0 [&::-webkit-calendar-picker-indicator]:w-full [&::-webkit-calendar-picker-indicator]:h-full [&::-webkit-calendar-picker-indicator]:cursor-pointer"
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
                                                            className="h-8 border-border/40 bg-zinc-900/50 font-mono text-[11px] pl-3 pr-10 cursor-pointer w-full
                                                    [&::-webkit-calendar-picker-indicator]:opacity-0 [&::-webkit-calendar-picker-indicator]:absolute [&::-webkit-calendar-picker-indicator]:inset-0 [&::-webkit-calendar-picker-indicator]:w-full [&::-webkit-calendar-picker-indicator]:h-full [&::-webkit-calendar-picker-indicator]:cursor-pointer"
                                                        />
                                                        <Calendar className="absolute right-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <Button
                                            className="w-full h-8 text-xs font-semibold cursor-pointer mt-auto"
                                            onClick={handleApplyCustomRange}
                                        >
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
                                                    className={`w-full text-left px-2 py-1.5 rounded-md text-xs font-medium transition-colors flex items-center justify-between cursor-pointer ${rangeType === 'all'
                                                        ? 'bg-primary/10 text-primary'
                                                        : 'text-zinc-400 hover:bg-muted/30 hover:text-zinc-200'
                                                        }`}
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
                                                            className={`w-full text-left px-2 py-1.5 rounded-md text-xs font-medium transition-colors flex items-center justify-between cursor-pointer ${isSelected
                                                                ? 'bg-primary/10 text-primary'
                                                                : 'text-zinc-400 hover:bg-muted/30 hover:text-zinc-200'
                                                                }`}
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
                </div>

                <div className="rounded-md border border-border/40 overflow-hidden flex-grow min-h-0 bg-zinc-950/20">
                    <Table wrapperClassName="h-full overflow-auto">
                        <TableHeader className="sticky top-0 bg-background z-10">
                            <TableRow className="hover:bg-transparent">
                                <TableHead className="w-[180px]">Timestamp</TableHead>
                                <TableHead>Action</TableHead>
                                <TableHead>Entity</TableHead>
                                <TableHead>Performed By</TableHead>
                                <TableHead className="text-right w-[80px]">Details</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {isLoading ? (
                                <TableRow>
                                    <TableCell colSpan={5} className="h-24">
                                        <div className="flex flex-col gap-2">
                                            <Skeleton className="h-4 w-full" />
                                            <Skeleton className="h-4 w-full" />
                                            <Skeleton className="h-4 w-full" />
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ) : items.length === 0 ? (
                                <TableRow>
                                    <TableCell colSpan={5} className="h-24 text-center text-muted-foreground">
                                        No audit logs for this environment.
                                    </TableCell>
                                </TableRow>
                            ) : (
                                items.map((log) => (
                                    <TableRow key={log.id} className="hover:bg-muted/30 text-sm">
                                        <TableCell className="text-muted-foreground whitespace-nowrap font-mono text-xs">
                                            {formatDate(log.timestamp)}
                                        </TableCell>
                                        <TableCell>
                                            <Badge variant="outline" className="text-[10px] font-mono uppercase">
                                                {log.action}
                                            </Badge>
                                        </TableCell>
                                        <TableCell className="font-mono text-xs text-primary/80">
                                            {log.entityName} ({log.entityFriendlyName || log.entityId})
                                        </TableCell>
                                        <TableCell className="text-muted-foreground whitespace-nowrap font-mono text-xs">
                                            {log.performedByEmail || log.performedBy}
                                        </TableCell>
                                        <TableCell className="text-right">
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                onClick={() => setSelectedLog(log)}
                                                disabled={!log.oldValues && !log.newValues}
                                                className="h-8 cursor-pointer"
                                            >
                                                View
                                            </Button>
                                        </TableCell>
                                    </TableRow>
                                ))
                            )}
                            {hasNextPage && (
                                <TableRow>
                                    <TableCell colSpan={5} className="h-14 text-center">
                                        <div ref={loadMoreRef} className="flex justify-center items-center h-full">
                                            <span className="text-sm text-muted-foreground animate-pulse">Loading more...</span>
                                        </div>
                                    </TableCell>
                                </TableRow>
                            )}
                        </TableBody>
                    </Table>
                </div>
            </div>

            {selectedLog && (
                <Dialog open={!!selectedLog} onOpenChange={(open) => !open && setSelectedLog(null)}>
                    <DialogContent
                        className="max-w-3xl max-h-[80vh] overflow-y-auto z-[110] border-border/40 bg-zinc-950">
                        <DialogHeader>
                            <DialogTitle>Audit Details</DialogTitle>
                            <DialogDescription>
                                {selectedLog.action} on {selectedLog.entityName} at {new Date(selectedLog.timestamp).toLocaleString()}
                            </DialogDescription>
                        </DialogHeader>
                        <div className="grid grid-cols-2 gap-4 mt-4">
                            <div className="space-y-2">
                                <h4 className="text-sm font-medium text-muted-foreground">Previous State</h4>
                                <div
                                    className="bg-zinc-950 p-4 rounded-md font-mono text-xs overflow-x-auto border border-border/40 text-emerald-500/80">
                                    {selectedLog.oldValues && selectedLog.oldValues !== "{}" ? (
                                        <pre>{JSON.stringify(JSON.parse(selectedLog.oldValues), null, 2)}</pre>
                                    ) : <span className="text-zinc-600">None</span>}
                                </div>
                            </div>
                            <div className="space-y-2">
                                <h4 className="text-sm font-medium text-muted-foreground">New State</h4>
                                <div
                                    className="bg-zinc-950 p-4 rounded-md font-mono text-xs overflow-x-auto border border-border/40 text-emerald-400">
                                    {selectedLog.newValues && selectedLog.newValues !== "{}" ? (
                                        <pre>{JSON.stringify(JSON.parse(selectedLog.newValues), null, 2)}</pre>
                                    ) : <span className="text-zinc-600">None</span>}
                                </div>
                            </div>
                        </div>
                        <DialogFooter>
                            <Button onClick={() => setSelectedLog(null)}>Close</Button>
                        </DialogFooter>
                    </DialogContent>
                </Dialog>
            )}
        </div>
    );
}

export function ProjectEnvironmentsTab({ project, isLoading }: { project?: ProjectDetails; isLoading: boolean }) {
    const navigate = useNavigate();
    const createEnvironment = useCreateEnvironment(project?.id || '');
    const cloneEnvironment = useCloneEnvironment(project?.id || '');
    const reorderEnvironments = useReorderEnvironments(project?.id || '');

    const [isCreateOpen, setIsCreateOpen] = useState(false);
    const [envToSync, setEnvToSync] = useState<string | null>(null);
    const [auditEnvId, setAuditEnvId] = useState<string | null>(null);

    const createForm = useForm<CreateEnvValues>({
        resolver: zodResolver(createEnvSchema),
        defaultValues: { name: '' }
    });

    const syncForm = useForm<SyncEnvValues>({
        resolver: zodResolver(syncEnvSchema),
        defaultValues: { sourceEnvId: '' }
    });

    const [localEnvs, setLocalEnvs] = useState<Environment[]>([]);
    const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

    const canManageProject = project?.userRole === ProjectRole.Owner || project?.userRole === ProjectRole.Admin;

    useEffect(() => {
        if (project?.environments) {
            setLocalEnvs(project.environments);
        }
    }, [project?.environments]);

    const handleCreateEnvSubmit = async (values: CreateEnvValues) => {
        try {
            await createEnvironment.mutateAsync(values.name.trim());
            toast.success('Environment created');
            createForm.reset({ name: '' });
            setIsCreateOpen(false);
        } catch (error: any) {
            handleApiError(error, createForm.setError, 'Failed to create environment');
        }
    };

    const handleSyncEnvironmentSubmit = async (values: SyncEnvValues) => {
        if (!envToSync) return;
        try {
            await cloneEnvironment.mutateAsync({ sourceEnvId: values.sourceEnvId, targetEnvId: envToSync });
            toast.success('Environment rules synchronized successfully');
            setEnvToSync(null);
        } catch (error: any) {
            handleApiError(error, syncForm.setError, 'Failed to sync rules');
        }
    };

    const handleDragStart = (index: number) => {
        setDraggedIndex(index);
    };

    const handleDragOver = (e: React.DragEvent, index: number) => {
        e.preventDefault();
        if (draggedIndex === null || draggedIndex === index) return;

        const newEnvs = [...localEnvs];
        const temp = newEnvs[draggedIndex];
        newEnvs[draggedIndex] = newEnvs[index];
        newEnvs[index] = temp;

        setDraggedIndex(index);
        setLocalEnvs(newEnvs);
    };

    const handleDragEnd = async () => {
        if (draggedIndex === null) return;
        setDraggedIndex(null);

        try {
            await reorderEnvironments.mutateAsync(localEnvs.map(e => e.id));
            toast.success('Environments order saved');
        } catch {
            toast.error('Failed to save environments order');
            if (project?.environments) {
                setLocalEnvs(project.environments);
            }
        }
    };

    return (
        <div className="space-y-6">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div>
                    <h2 className="text-2xl font-bold tracking-tight">Environments</h2>
                    <p className="text-muted-foreground">Manage environments for this project.</p>
                </div>
                {canManageProject && (
                    <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
                        <DialogTrigger asChild>
                            <Button className="cursor-pointer">
                                <Plus className="mr-2 h-4 w-4" />
                                New Environment
                            </Button>
                        </DialogTrigger>
                        <DialogContent className="border-border/40 bg-zinc-950">
                            <DialogHeader>
                                <DialogTitle>Create Environment</DialogTitle>
                                <DialogDescription>
                                    Environments have separate feature flags and API keys.
                                </DialogDescription>
                            </DialogHeader>
                            <Form {...createForm}>
                                <form onSubmit={createForm.handleSubmit(handleCreateEnvSubmit)}>
                                    <div className="py-4">
                                        <FormField
                                            control={createForm.control}
                                            name="name"
                                            render={({ field }) => (
                                                <FormItem>
                                                    <FormControl>
                                                        <Input
                                                            {...field}
                                                            placeholder="e.g., Production, Staging"
                                                            autoFocus
                                                        />
                                                    </FormControl>
                                                    <FormMessage />
                                                </FormItem>
                                            )}
                                        />
                                    </div>
                                    <DialogFooter>
                                        <Button type="button" variant="outline" onClick={() => setIsCreateOpen(false)}>Cancel</Button>
                                        <Button type="submit" disabled={createEnvironment.isPending}>
                                            {createEnvironment.isPending ? 'Creating...' : 'Create'}
                                        </Button>
                                    </DialogFooter>
                                </form>
                            </Form>
                        </DialogContent>
                    </Dialog>
                )}
            </div>

            <div className="grid gap-4">
                {isLoading ? (
                    Array.from({ length: 2 }).map((_, i) => (
                        <Card key={i} className="border-border/40 bg-zinc-950/20">
                            <CardContent className="p-5 flex items-center justify-between">
                                <div className="flex items-center gap-4 w-full">
                                    <Skeleton className="h-5 w-5 rounded-full shrink-0" />
                                    <div className="space-y-2 flex-1">
                                        <Skeleton className="h-5 w-[180px] rounded" />
                                        <Skeleton className="h-4 w-[120px] rounded" />
                                    </div>
                                    <div className="flex items-center gap-2">
                                        <Skeleton className="h-9 w-20 rounded" />
                                        <Skeleton className="h-9 w-24 rounded" />
                                    </div>
                                </div>
                            </CardContent>
                        </Card>
                    ))
                ) : (
                    localEnvs.map((env: Environment, index: number) => {
                        const canManageEnv = env.userRole === ProjectRole.Owner || env.userRole === ProjectRole.Admin;
                        const activeKeysCount = env.keys?.length || 0;
                        const isDragging = draggedIndex === index;

                        return (
                            <Card
                                key={env.id}
                                draggable={canManageProject && localEnvs.length > 1}
                                onDragStart={() => handleDragStart(index)}
                                onDragOver={(e) => handleDragOver(e, index)}
                                onDragEnd={handleDragEnd}
                                className={`border-border/40 bg-zinc-950/20 shadow-md group ${isDragging ? 'opacity-40 border-dashed border-primary/40' : ''
                                    } ${canManageProject && localEnvs.length > 1 ? 'cursor-grab active:cursor-grabbing' : ''
                                    } cursor-pointer hover:bg-zinc-950/40 hover:border-primary/20 transition-all`}
                                onClick={() => {
                                    navigate(`/projects/${project?.id}/environments/${env.id}`);
                                }}
                            >
                                <CardContent className="p-5 flex items-center justify-between">
                                    <div className="flex items-center gap-4">
                                        {canManageProject && localEnvs.length > 1 && (
                                            <div
                                                className="flex items-center text-muted-foreground/30 group-hover:text-muted-foreground/60 transition-colors pr-2 border-r border-border/10 shrink-0">
                                                <GripVertical className="h-5 w-5" />
                                            </div>
                                        )}

                                        <div className="space-y-1.5">
                                            <div className="flex items-center gap-2.5">
                                                <Box className="h-5 w-5 text-muted-foreground" />
                                                <span
                                                    className="font-semibold text-lg tracking-tight group-hover:text-primary transition-colors">{env.name}</span>
                                                <Badge variant="outline"
                                                    className={`text-[9px] font-mono font-semibold uppercase px-1.5 py-0.5 ${getEnvBadgeStyle(env.name)}`}>
                                                    {env.name}
                                                </Badge>
                                            </div>
                                            {canManageEnv && (
                                                <div
                                                    className="flex items-center gap-1.5 text-xs text-muted-foreground font-mono">
                                                    <Key className="h-3.5 w-3.5" />
                                                    {activeKeysCount} active API key(s)
                                                </div>
                                            )}
                                        </div>
                                    </div>

                                    <div className="flex items-center gap-2" onClick={(e) => e.stopPropagation()}>
                                        {env.userRole < 3 && (
                                            <Button variant="outline" size="sm" onClick={() => setAuditEnvId(env.id)}
                                                className="h-9 px-3 text-xs font-medium cursor-pointer">
                                                <FileClock className="mr-1.5 h-3.5 w-3.5" />
                                                Logs
                                            </Button>
                                        )}
                                        {canManageEnv && (
                                            <Button variant="outline" size="sm" onClick={() => {
                                                setEnvToSync(env.id);
                                                syncForm.reset({ sourceEnvId: '' });
                                            }} className="h-9 px-3 text-xs font-medium cursor-pointer">
                                                <ArrowRightLeft className="mr-1.5 h-3.5 w-3.5" />
                                                Sync
                                            </Button>
                                        )}
                                        {canManageEnv ? (
                                            <Button variant="default" size="sm"
                                                onClick={() => navigate(`/projects/${project?.id}/environments/${env.id}`)}
                                                className="h-9 px-3 text-xs font-medium cursor-pointer">
                                                <Settings className="mr-1.5 h-3.5 w-3.5" />
                                                Configure
                                            </Button>
                                        ) : (
                                            <Button variant="default" size="sm"
                                                onClick={() => navigate(`/projects/${project?.id}/environments/${env.id}`)}
                                                className="h-9 px-3 text-xs font-medium cursor-pointer">
                                                <Settings className="mr-1.5 h-3.5 w-3.5" />
                                                View
                                            </Button>
                                        )}
                                    </div>
                                </CardContent>
                            </Card>
                        )
                    }
                    ))}
            </div>

            <Dialog open={!!auditEnvId} onOpenChange={(open) => !open && setAuditEnvId(null)}>
                <DialogContent className="max-w-5xl h-[38rem] flex flex-col border-border/40 bg-zinc-950">
                    <DialogHeader className="shrink-0">
                        <DialogTitle>Environment Activity Log</DialogTitle>
                        <DialogDescription>
                            Recent changes made to flags and settings in this environment.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="flex-1 min-h-0">
                        {auditEnvId && <EnvironmentAuditLogs envId={auditEnvId} />}
                    </div>
                </DialogContent>
            </Dialog>

            <Dialog open={!!envToSync} onOpenChange={(open) => !open && setEnvToSync(null)}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle>Sync Rules from Another Environment</DialogTitle>
                        <DialogDescription>
                            This will overwrite all current flag rules in this environment with the rules from the
                            selected source environment.
                        </DialogDescription>
                    </DialogHeader>
                    <Form {...syncForm}>
                        <form onSubmit={syncForm.handleSubmit(handleSyncEnvironmentSubmit)}>
                            <div className="py-4">
                                <FormField
                                    control={syncForm.control}
                                    name="sourceEnvId"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormControl>
                                                <Select value={field.value} onValueChange={field.onChange}>
                                                    <SelectTrigger>
                                                        <SelectValue placeholder="Select source environment" />
                                                    </SelectTrigger>
                                                    <SelectContent>
                                                        {project?.environments
                                                            ?.filter(e => e.id !== envToSync)
                                                            .map(e => (
                                                                <SelectItem key={e.id} value={e.id}>{e.name}</SelectItem>
                                                            ))
                                                        }
                                                    </SelectContent>
                                                </Select>
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                            </div>
                            <DialogFooter>
                                <Button type="button" variant="outline" onClick={() => setEnvToSync(null)}>Cancel</Button>
                                <Button type="submit" disabled={cloneEnvironment.isPending}>
                                    {cloneEnvironment.isPending ? 'Syncing...' : 'Sync Rules'}
                                </Button>
                            </DialogFooter>
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>
        </div>
    );
}