import { useEffect, useState, useRef, useCallback, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { ToggleRight, Trash2, MoreHorizontal, Settings2, Copy, ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react';
import { useProjectFlags, useToggleFeatureFlag, useUpdateFlagPrivacy, useDeleteFeatureFlag } from '@/api/queries';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { Switch } from '@/components/ui/switch';
import { Badge } from '@/components/ui/badge';
import { toast } from 'sonner';
import { ProjectRole } from '@/api/types';
import { TableSkeleton } from '@/components/TableSkeleton';
import { useVirtualizer } from '@tanstack/react-virtual';

const ROW_HEIGHT = 53;

interface ProjectFlagsTabProps {
    project: {
        id: string;
        userRole: ProjectRole;
        environments: Array<{ id: string; name: string; userRole?: ProjectRole }>;
    };
    search: string;
    tags: string[];
    sortBy: string;
    isLoadingProject?: boolean;
}

export function ProjectFlagsTab({ project, search, tags, sortBy, isLoadingProject }: ProjectFlagsTabProps) {
    const navigate = useNavigate();
    const scrollContainerRef = useRef<HTMLDivElement>(null);

    const { data: flags, isLoading: isLoadingFlags } = useProjectFlags(project.id, search, tags);
    const toggleFlag = useToggleFeatureFlag(project.id);
    const updatePrivacy = useUpdateFlagPrivacy(project.id);
    const deleteFlag = useDeleteFeatureFlag(project.id);

    const [isDeleteOpen, setIsDeleteOpen] = useState(false);
    const [flagToDelete, setFlagToDelete] = useState<string | null>(null);
    const [envSort, setEnvSort] = useState<{ envId: string; direction: 'desc' | 'asc' } | null>(null);
    const [clientSideSort, setClientSideSort] = useState<'desc' | 'asc' | null>(null);

    const canEditFlags = project.userRole === ProjectRole.Owner || project.userRole === ProjectRole.Admin || project.userRole === ProjectRole.Editor;
    const showSkeleton = isLoadingFlags || isLoadingProject;

    const flagIdsString = flags?.map(f => f.id).sort().join(',') || '';

    const orderedFlagIds = useMemo(() => {
        if (!flags || flags.length === 0) return [];

        return [...flags].sort((a, b) => {
            if (clientSideSort) {
                const stateA = a.isClientSideExposed;
                const stateB = b.isClientSideExposed;

                if (stateA !== stateB) {
                    return clientSideSort === 'desc'
                        ? (stateA ? -1 : 1)
                        : (stateA ? 1 : -1);
                }
            }

            if (envSort) {
                const stateA = a.environments.find(e => e.environmentId === envSort.envId)?.isEnabled || false;
                const stateB = b.environments.find(e => e.environmentId === envSort.envId)?.isEnabled || false;

                if (stateA !== stateB) {
                    return envSort.direction === 'desc' ? (stateA ? -1 : 1) : (stateA ? 1 : -1);
                }
            }

            if (sortBy === 'updated-desc') {
                const dateA = a.updatedAt ? new Date(a.updatedAt).getTime() : new Date(a.createdAt).getTime();
                const dateB = b.updatedAt ? new Date(b.updatedAt).getTime() : new Date(b.createdAt).getTime();
                return dateB - dateA;
            }
            if (sortBy === 'updated-asc') {
                const dateA = a.updatedAt ? new Date(a.updatedAt).getTime() : new Date(a.createdAt).getTime();
                const dateB = b.updatedAt ? new Date(b.updatedAt).getTime() : new Date(b.createdAt).getTime();
                return dateA - dateB;
            }
            if (sortBy === 'key-asc') return a.key.localeCompare(b.key);
            if (sortBy === 'key-desc') return b.key.localeCompare(a.key);
            if (sortBy === 'date-desc') return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
            if (sortBy === 'date-asc') return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();

            return 0;
        }).map(f => f.id);
    }, [flagIdsString, sortBy, envSort, clientSideSort]);

    const orderedFlags = useMemo(() => {
        if (!flags) return [];
        const flagsById = new Map(flags.map(f => [f.id, f]));
        return orderedFlagIds.map(id => flagsById.get(id)).filter(Boolean) as typeof flags;
    }, [orderedFlagIds, flags]);

    const rowVirtualizer = useVirtualizer({
        count: orderedFlags.length,
        getScrollElement: () => scrollContainerRef.current,
        estimateSize: () => ROW_HEIGHT,
        overscan: 10,
    });

    useEffect(() => {
        scrollContainerRef.current?.scrollTo({ top: 0 });
    }, [search, tags, sortBy]);

    const confirmDeleteFlag = useCallback((flagKey: string) => {
        setFlagToDelete(flagKey);
        setIsDeleteOpen(true);
    }, []);

    const handleDeleteExecute = async () => {
        if (!flagToDelete) return;
        try {
            await deleteFlag.mutateAsync(flagToDelete);
            setIsDeleteOpen(false);
            setFlagToDelete(null);
            toast.success('Feature flag globally deleted');
        } catch {
            toast.error('Failed to delete feature flag');
        }
    };

    const handleToggle = useCallback(async (envId: string, flagKey: string, targetValue: boolean) => {
        try {
            await toggleFlag.mutateAsync({ envId, flagKey, isEnabled: targetValue });
            toast.success(`Flag ${targetValue ? 'enabled' : 'disabled'} for environment`);
        } catch {
            toast.error('Failed to toggle flag');
        }
    }, [toggleFlag]);

    const handleTogglePrivacy = useCallback(async (flagKey: string, isExposed: boolean) => {
        try {
            await updatePrivacy.mutateAsync({ flagKey, isClientSideExposed: isExposed });
            toast.success(`Flag is now ${isExposed ? 'exposed to client SDKs' : 'server-side only'}`);
        } catch {
            toast.error('Failed to update flag privacy');
        }
    }, [updatePrivacy]);

    const handleCopyKey = useCallback((key: string) => {
        navigator.clipboard.writeText(key);
        toast.success('Flag key copied to clipboard');
    }, []);

    const handleEnvSortClick = useCallback((envId: string) => {
        setClientSideSort(null);
        setEnvSort(prev => {
            if (prev?.envId !== envId) return { envId, direction: 'desc' };
            if (prev.direction === 'desc') return { envId, direction: 'asc' };
            return null;
        });
    }, []);

    const handleClientSideSortClick = useCallback(() => {
        setEnvSort(null);
        setClientSideSort(prev => {
            if (prev === null) return 'desc';
            if (prev === 'desc') return 'asc';
            return null;
        });
    }, []);

    const virtualItems = rowVirtualizer.getVirtualItems();
    const totalSize = rowVirtualizer.getTotalSize();

    return (
        <div className="space-y-6">
            <Card className="border-border/40 bg-zinc-950/20">
                <div
                    ref={scrollContainerRef}
                    className="max-h-[calc(100vh-240px)] overflow-auto flags-table-scroll-container"
                >
                    <Table wrapperClassName="overflow-visible">
                        <TableHeader className="sticky top-0 bg-background z-10 h-[41px] shadow-sm">
                            <TableRow className="hover:bg-transparent border-border/40 shadow-sm h-10">
                                <TableHead className="w-[280px]">Flag Key</TableHead>
                                <TableHead
                                    className="w-[120px] text-center cursor-pointer select-none hover:bg-muted/30 transition-colors group"
                                    onClick={handleClientSideSortClick}
                                >
                                    <div className="flex items-center justify-center gap-1.5">
                                        <span>Client Side</span>
                                        <div className="w-3 h-3 flex items-center justify-center text-muted-foreground transition-opacity">
                                            {clientSideSort ? (
                                                clientSideSort === 'desc'
                                                    ? <ArrowDown className="h-3 w-3 text-primary" />
                                                    : <ArrowUp className="h-3 w-3 text-primary" />
                                            ) : (
                                                <ArrowUpDown className="h-3 w-3 opacity-0 group-hover:opacity-50" />
                                            )}
                                        </div>
                                    </div>
                                </TableHead>

                                {project.environments?.map(env => (
                                    <TableHead
                                        key={env.id}
                                        className="text-center cursor-pointer select-none hover:bg-muted/30 transition-colors group"
                                        onClick={() => handleEnvSortClick(env.id)}
                                    >
                                        <div className="flex items-center justify-center gap-1.5">
                                            <span>{env.name}</span>
                                            <div className="w-3 h-3 flex items-center justify-center text-muted-foreground transition-opacity">
                                                {envSort?.envId === env.id ? (
                                                    envSort.direction === 'desc'
                                                        ? <ArrowDown className="h-3 w-3 text-primary" />
                                                        : <ArrowUp className="h-3 w-3 text-primary" />
                                                ) : (
                                                    <ArrowUpDown className="h-3 w-3 opacity-0 group-hover:opacity-50" />
                                                )}
                                            </div>
                                        </div>
                                    </TableHead>
                                ))}

                                <TableHead className="text-right w-[80px]">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {showSkeleton ? (
                                <TableSkeleton columnsCount={(project.environments?.length || 0) + 3} rowsCount={3} />
                            ) : orderedFlags.length === 0 ? (
                                <TableRow className="border-border/40 h-[53px]">
                                    <TableCell colSpan={(project.environments?.length || 0) + 3} className="h-24 text-center text-muted-foreground">
                                        No feature flags found in this project.
                                    </TableCell>
                                </TableRow>
                            ) : (
                                <>
                                    {virtualItems.length > 0 && virtualItems[0].start > 0 && (
                                        <tr>
                                            <td
                                                colSpan={(project.environments?.length || 0) + 3}
                                                style={{ height: virtualItems[0].start, padding: 0, border: 'none' }}
                                            />
                                        </tr>
                                    )}

                                    {virtualItems.map((virtualRow) => {
                                        const flag = orderedFlags[virtualRow.index];
                                        if (!flag) return null;
                                        return (
                                            <TableRow
                                                key={flag.id}
                                                data-index={virtualRow.index}
                                                className="border-border/40 hover:bg-muted/30 cursor-pointer group"
                                                style={{ height: ROW_HEIGHT }}
                                                onClick={() => navigate(`/projects/${project.id}/flags/${flag.key}`)}
                                            >
                                                <TableCell className="font-medium font-mono text-sm py-2">
                                                    <div className="flex items-center gap-2">
                                                        <ToggleRight className="h-4 w-4 text-muted-foreground shrink-0" />
                                                        <div className="flex flex-col">
                                                            <span className="truncate max-w-[200px]">{flag.key}</span>
                                                            {flag.name && <span className="text-[10px] text-muted-foreground font-sans mt-0.5">{flag.name}</span>}
                                                            {flag.tags && flag.tags.length > 0 && (
                                                                <div className="flex gap-1.5 mt-1.5 flex-wrap max-w-[200px]" onClick={(e) => e.stopPropagation()}>
                                                                    {flag.tags.slice(0, 3).map(tag => (
                                                                        <Badge key={tag} variant="outline" className="text-[9px] h-4 font-sans font-medium uppercase bg-zinc-900/60 text-zinc-400 border-zinc-800/80 px-1.5 py-0">
                                                                            {tag}
                                                                        </Badge>
                                                                    ))}
                                                                    {flag.tags.length > 3 && (
                                                                        <Badge variant="outline" title={flag.tags.slice(3).join(', ')} className="text-[9px] h-4 font-sans font-medium uppercase bg-zinc-900/60 text-zinc-400 border-zinc-800/80 px-1.5 py-0 cursor-help">
                                                                            +{flag.tags.length - 3}
                                                                        </Badge>
                                                                    )}
                                                                </div>
                                                            )}
                                                        </div>
                                                    </div>
                                                </TableCell>

                                                <TableCell className="text-center py-2" onClick={(e) => e.stopPropagation()}>
                                                    <div className="flex flex-col items-center justify-center gap-1">
                                                        <Switch
                                                            checked={flag.isClientSideExposed}
                                                            onCheckedChange={(checked) => handleTogglePrivacy(flag.key, checked)}
                                                            disabled={updatePrivacy.isPending || !canEditFlags}
                                                        />
                                                    </div>
                                                </TableCell>

                                                {project.environments?.map(env => {
                                                    const state = flag.environments.find(e => e.environmentId === env.id);
                                                    const isEnabled = state?.isEnabled || false;
                                                    const effectiveEnvRole = env.userRole !== undefined ? env.userRole : project.userRole;
                                                    const canEditEnv = effectiveEnvRole === ProjectRole.Owner || effectiveEnvRole === ProjectRole.Admin || effectiveEnvRole === ProjectRole.Editor;

                                                    return (
                                                        <TableCell key={env.id} className="text-center py-2" onClick={(e) => e.stopPropagation()}>
                                                            <div className="inline-flex items-center justify-center">
                                                                <Switch
                                                                    checked={isEnabled}
                                                                    onCheckedChange={(checked) => handleToggle(env.id, flag.key, checked)}
                                                                    disabled={toggleFlag.isPending || !canEditEnv}
                                                                />
                                                            </div>
                                                        </TableCell>
                                                    );
                                                })}

                                                <TableCell className="text-right py-2" onClick={(e) => e.stopPropagation()}>
                                                    <div className="flex items-center justify-end gap-2 pr-2">
                                                        <DropdownMenu>
                                                            <DropdownMenuTrigger asChild>
                                                                <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-foreground hover:bg-muted/50 rounded-md cursor-pointer">
                                                                    <MoreHorizontal className="h-4 w-4" />
                                                                </Button>
                                                            </DropdownMenuTrigger>
                                                            <DropdownMenuContent align="end" className="border-border/40 bg-zinc-950">
                                                                <DropdownMenuItem onClick={() => navigate(`/projects/${project.id}/flags/${flag.key}`)} className="cursor-pointer">
                                                                    <Settings2 className="mr-2 h-4 w-4" /> Configure rules
                                                                </DropdownMenuItem>
                                                                <DropdownMenuItem onClick={() => handleCopyKey(flag.key)} className="cursor-pointer">
                                                                    <Copy className="mr-2 h-4 w-4" /> Copy key
                                                                </DropdownMenuItem>
                                                                {canEditFlags && (
                                                                    <>
                                                                        <DropdownMenuSeparator className="bg-border/40" />
                                                                        <DropdownMenuItem onClick={() => confirmDeleteFlag(flag.key)} className="text-destructive focus:text-destructive cursor-pointer">
                                                                            <Trash2 className="mr-2 h-4 w-4" /> Delete flag
                                                                        </DropdownMenuItem>
                                                                    </>
                                                                )}
                                                            </DropdownMenuContent>
                                                        </DropdownMenu>
                                                    </div>
                                                </TableCell>
                                            </TableRow>
                                        );
                                    })}

                                    {virtualItems.length > 0 && (
                                        <tr>
                                            <td
                                                colSpan={(project.environments?.length || 0) + 3}
                                                style={{
                                                    height: totalSize - (virtualItems[virtualItems.length - 1].end),
                                                    padding: 0,
                                                    border: 'none',
                                                }}
                                            />
                                        </tr>
                                    )}
                                </>
                            )}
                        </TableBody>
                    </Table>
                </div>
            </Card>

            <Dialog open={isDeleteOpen} onOpenChange={setIsDeleteOpen}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle className="text-destructive">Delete Feature Flag</DialogTitle>
                        <DialogDescription>
                            Are you sure you want to globally delete this flag? This will instantly remove it from ALL environments and cascaded rules. SDK evaluations will stop receiving this flag.
                        </DialogDescription>
                    </DialogHeader>
                    <DialogFooter className="mt-4">
                        <Button variant="outline" onClick={() => setIsDeleteOpen(false)}>Cancel</Button>
                        <Button variant="destructive" onClick={handleDeleteExecute} disabled={deleteFlag.isPending}>
                            {deleteFlag.isPending ? 'Deleting...' : 'Delete Globally'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}