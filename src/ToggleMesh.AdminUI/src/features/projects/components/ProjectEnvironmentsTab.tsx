import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Box, ArrowRightLeft, FileClock, Settings, Key, GripVertical } from 'lucide-react';
import { useCreateEnvironment, useCloneEnvironment, useAuditLogs, useReorderEnvironments } from '@/api/queries';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
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

function EnvironmentAuditLogs({ envId }: { envId: string }) {
    const pageSize = 6;

    const [filterAction, setFilterAction] = useState<string>('all');
    const [filterEntity, setFilterEntity] = useState<string>('all');
    const [sortOrder, setSortOrder] = useState<string>('desc');

    const { 
        data, 
        isLoading, 
        fetchNextPage, 
        hasNextPage, 
        isFetchingNextPage 
    } = useAuditLogs(envId, pageSize, filterAction, filterEntity, sortOrder);
    
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
                <div className="flex gap-4 shrink-0">
                    <Select value={filterAction} onValueChange={handleActionChange}>
                        <SelectTrigger className="w-[150px] bg-zinc-950/20 border-border/40 text-xs">
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
                        <SelectTrigger className="w-[150px] bg-zinc-950/20 border-border/40 text-xs">
                            <SelectValue placeholder="Entity" />
                        </SelectTrigger>
                        <SelectContent className="border-border/40 bg-zinc-950">
                            <SelectItem value="all">All Entities</SelectItem>
                            <SelectItem value="flagenvironmentstate">Flags Status</SelectItem>
                            <SelectItem value="flagrule">Rules</SelectItem>
                        </SelectContent>
                    </Select>

                    <Select value={sortOrder} onValueChange={handleSortChange}>
                        <SelectTrigger className="w-[150px] bg-zinc-950/20 border-border/40 text-xs font-mono">
                            <SelectValue placeholder="Sort" />
                        </SelectTrigger>
                        <SelectContent className="border-border/40 bg-zinc-950">
                            <SelectItem value="desc">Newest First</SelectItem>
                            <SelectItem value="asc">Oldest First</SelectItem>
                        </SelectContent>
                    </Select>
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
    const [newEnvName, setNewEnvName] = useState('');

    const [envToSync, setEnvToSync] = useState<string | null>(null);
    const [syncSourceEnv, setSyncSourceEnv] = useState<string>('');

    const [auditEnvId, setAuditEnvId] = useState<string | null>(null);

    const [localEnvs, setLocalEnvs] = useState<Environment[]>([]);
    const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

    const canManageProject = project?.userRole === ProjectRole.Owner || project?.userRole === ProjectRole.Admin;

    useEffect(() => {
        if (project?.environments) {
            setLocalEnvs(project.environments);
        }
    }, [project?.environments]);

    const handleCreateEnv = async () => {
        if (!newEnvName.trim()) return;
        try {
            await createEnvironment.mutateAsync(newEnvName);
            toast.success('Environment created');
            setNewEnvName('');
            setIsCreateOpen(false);
        } catch {
            toast.error('Failed to create environment');
        }
    };

    const handleSyncEnvironment = async () => {
        if (!envToSync || !syncSourceEnv) return;
        try {
            await cloneEnvironment.mutateAsync({ sourceEnvId: syncSourceEnv, targetEnvId: envToSync });
            toast.success('Environment rules synchronized successfully');
            setEnvToSync(null);
        } catch {
            toast.error('Failed to sync rules');
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
                            <div className="py-4">
                                <Input
                                    placeholder="e.g., Production, Staging"
                                    value={newEnvName}
                                    onChange={(e) => setNewEnvName(e.target.value)}
                                    onKeyDown={(e) => {
                                        if (e.key === 'Enter' && !createEnvironment.isPending && newEnvName.trim()) {
                                            handleCreateEnv();
                                        }
                                    }}
                                    autoFocus
                                />
                            </div>
                            <DialogFooter>
                                <Button variant="outline" onClick={() => setIsCreateOpen(false)}>Cancel</Button>
                                <Button onClick={handleCreateEnv}
                                    disabled={createEnvironment.isPending || !newEnvName.trim()}>
                                    {createEnvironment.isPending ? 'Creating...' : 'Create'}
                                </Button>
                            </DialogFooter>
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
                                className={`border-border/40 bg-zinc-950/20 hover:bg-zinc-950/40 hover:border-primary/20 transition-all shadow-md group ${isDragging ? 'opacity-40 border-dashed border-primary/40' : ''
                                    } ${canManageProject && localEnvs.length > 1 ? 'cursor-grab active:cursor-grabbing' : 'cursor-pointer'
                                    }`}
                                onClick={() => navigate(`/projects/${project?.id}/environments/${env.id}`)}
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
                                            <div
                                                className="flex items-center gap-1.5 text-xs text-muted-foreground font-mono">
                                                <Key className="h-3.5 w-3.5" />
                                                {activeKeysCount} active API key(s)
                                            </div>
                                        </div>
                                    </div>

                                    <div className="flex items-center gap-2" onClick={(e) => e.stopPropagation()}>
                                        <Button variant="outline" size="sm" onClick={() => setAuditEnvId(env.id)}
                                            className="h-9 px-3 text-xs font-medium cursor-pointer">
                                            <FileClock className="mr-1.5 h-3.5 w-3.5" />
                                            Logs
                                        </Button>
                                        {canManageEnv && (
                                            <Button variant="outline" size="sm" onClick={() => {
                                                setEnvToSync(env.id);
                                                setSyncSourceEnv('');
                                            }} className="h-9 px-3 text-xs font-medium cursor-pointer">
                                                <ArrowRightLeft className="mr-1.5 h-3.5 w-3.5" />
                                                Sync
                                            </Button>
                                        )}
                                        <Button variant="default" size="sm"
                                            onClick={() => navigate(`/projects/${project?.id}/environments/${env.id}`)}
                                            className="h-9 px-3 text-xs font-medium cursor-pointer">
                                            <Settings className="mr-1.5 h-3.5 w-3.5" />
                                            Configure
                                        </Button>
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
                    <div className="py-4">
                        <Select value={syncSourceEnv} onValueChange={setSyncSourceEnv}>
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
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setEnvToSync(null)}>Cancel</Button>
                        <Button onClick={handleSyncEnvironment} disabled={cloneEnvironment.isPending || !syncSourceEnv}>
                            {cloneEnvironment.isPending ? 'Syncing...' : 'Sync Rules'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}