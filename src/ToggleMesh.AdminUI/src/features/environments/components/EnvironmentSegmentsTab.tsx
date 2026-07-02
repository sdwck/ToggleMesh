import { useState } from 'react';
import { useSegments, useCreateSegment, useUpdateSegment, useDeleteSegment } from '@/api/queries';
import type { SegmentDto } from '@/api/types';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { Plus, Users, Edit2, Trash2, MoreHorizontal } from 'lucide-react';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle
} from '@/components/ui/dialog';

import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import { toast } from 'sonner';
import { SegmentEditorDialog } from './SegmentEditorDialog';

export function EnvironmentSegmentsTab({ projectId, environmentId, canManage }: { projectId: string; environmentId: string; canManage: boolean }) {
    const { data: segments, isLoading } = useSegments(projectId, environmentId);
    const createSegment = useCreateSegment();
    const updateSegment = useUpdateSegment();
    const deleteSegment = useDeleteSegment();

    const [isCreateOpen, setIsCreateOpen] = useState(false);

    const [editingSegment, setEditingSegment] = useState<SegmentDto | null>(null);
    const [viewingSegment, setViewingSegment] = useState<SegmentDto | null>(null);

    const [segmentToDelete, setSegmentToDelete] = useState<SegmentDto | null>(null);

    const handleCreateSubmit = async (data: any) => {
        try {
            await createSegment.mutateAsync({
                projectId,
                environmentId,
                data: { name: data.name, description: data.description, rules: data.rules }
            });
            setIsCreateOpen(false);
            toast.success('Segment created successfully');
        } catch {
            toast.error('Failed to create segment');
        }
    };

    const handleUpdateSubmit = async (data: any) => {
        if (!editingSegment) return;
        try {
            await updateSegment.mutateAsync({
                projectId,
                environmentId,
                segmentId: editingSegment.id,
                data: { name: data.name, description: data.description, rules: data.rules }
            });
            setEditingSegment(null);
            toast.success('Segment updated successfully');
        } catch {
            toast.error('Failed to update segment');
        }
    };

    const handleDelete = async () => {
        if (!segmentToDelete) return;
        try {
            await deleteSegment.mutateAsync({
                projectId,
                environmentId,
                segmentId: segmentToDelete.id
            });
            setSegmentToDelete(null);
            toast.success('Segment deleted successfully');
        } catch {
            toast.error('Failed to delete segment');
        }
    };

    return (
        <Card className="border-border/40 bg-zinc-950/20">
            <CardHeader className="flex flex-row items-center justify-between space-y-0">
                <div>
                    <CardTitle className="flex items-center gap-2 text-lg">
                        <Users className="h-5 w-5 text-primary" />
                        Segments
                    </CardTitle>
                    <CardDescription className="mt-1.5">
                        Target multiple flags to specific groups of users defined by these segments.
                    </CardDescription>
                </div>
                {canManage && (
                    <Button onClick={() => setIsCreateOpen(true)} size="sm">
                        <Plus className="mr-2 h-4 w-4" />
                        Create Segment
                    </Button>
                )}
            </CardHeader>
            <CardContent>
                <Table>
                    <TableHeader>
                        <TableRow className="border-border/40">
                            <TableHead>Name</TableHead>
                            <TableHead>Description</TableHead>
                            <TableHead>Rules</TableHead>
                            {canManage && <TableHead className="text-right w-[80px]">Actions</TableHead>}
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {isLoading ? (
                            Array.from({ length: 3 }).map((_, i) => (
                                <TableRow key={i} className="h-[53px] border-border/40">
                                    <TableCell><Skeleton className="h-5 w-[150px] rounded" /></TableCell>
                                    <TableCell><Skeleton className="h-5 w-[250px] rounded" /></TableCell>
                                    <TableCell><Skeleton className="h-5 w-16 rounded" /></TableCell>
                                    {canManage && (
                                        <TableCell className="text-right">
                                            <MoreHorizontal className="h-4 w-4 text-zinc-800 ml-auto animate-pulse" />
                                        </TableCell>
                                    )}
                                </TableRow>
                            ))
                        ) : segments && segments.length > 0 ? (
                            segments.map((segment) => (
                                <TableRow 
                                    key={segment.id} 
                                    className="border-border/40 hover:bg-muted/10 h-[53px] group cursor-pointer"
                                    onClick={() => {
                                        if (canManage) {
                                            setEditingSegment(segment);
                                        } else {
                                            setViewingSegment(segment);
                                        }
                                    }}
                                >
                                    <TableCell className="font-medium text-primary">{segment.name}</TableCell>
                                    <TableCell className="text-muted-foreground text-sm">{segment.description || 'No description'}</TableCell>
                                    <TableCell className="text-muted-foreground text-xs font-mono">{segment.rules.length} rule(s)</TableCell>
                                    {canManage && (
                                        <TableCell className="text-right" onClick={(e) => e.stopPropagation()}>
                                            <DropdownMenu>
                                                <DropdownMenuTrigger asChild>
                                                    <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-foreground cursor-pointer rounded-md">
                                                        <MoreHorizontal className="h-4 w-4" />
                                                    </Button>
                                                </DropdownMenuTrigger>
                                                <DropdownMenuContent align="end" className="border-border/40 bg-zinc-950">
                                                    <DropdownMenuItem onSelect={() => {
                                                        setTimeout(() => {
                                                            setEditingSegment(segment);
                                                        }, 50);
                                                    }} className="cursor-pointer">
                                                        <Edit2 className="mr-2 h-4 w-4" /> Edit
                                                    </DropdownMenuItem>
                                                    <DropdownMenuItem onSelect={() => {
                                                        setTimeout(() => {
                                                            setSegmentToDelete(segment);
                                                        }, 50);
                                                    }} className="text-destructive focus:text-destructive cursor-pointer">
                                                        <Trash2 className="mr-2 h-4 w-4" /> Delete
                                                    </DropdownMenuItem>
                                                </DropdownMenuContent>
                                            </DropdownMenu>
                                        </TableCell>
                                    )}
                                </TableRow>
                            ))
                        ) : (
                            <TableRow>
                                <TableCell colSpan={canManage ? 4 : 3} className="h-24 text-center text-muted-foreground">
                                    No segments found.
                                </TableCell>
                            </TableRow>
                        )}
                    </TableBody>
                </Table>
            </CardContent>

            <SegmentEditorDialog 
                open={isCreateOpen}
                onOpenChange={setIsCreateOpen}
                mode="create"
                segment={null}
                onSave={handleCreateSubmit}
                isSaving={createSegment.isPending}
            />

            <SegmentEditorDialog 
                open={!!editingSegment}
                onOpenChange={(open) => !open && setEditingSegment(null)}
                mode="edit"
                segment={editingSegment}
                onSave={handleUpdateSubmit}
                isSaving={updateSegment.isPending}
            />

            <SegmentEditorDialog 
                open={!!viewingSegment}
                onOpenChange={(open) => !open && setViewingSegment(null)}
                mode="view"
                segment={viewingSegment}
                onSave={async () => {}}
                isSaving={false}
            />

            <Dialog open={!!segmentToDelete} onOpenChange={(o) => !o && setSegmentToDelete(null)}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle className="text-destructive">Delete Segment</DialogTitle>
                        <DialogDescription>
                            Are you sure you want to delete segment "{segmentToDelete?.name}"? Flags using this segment will no longer evaluate correctly for these rules.
                        </DialogDescription>
                    </DialogHeader>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setSegmentToDelete(null)}>Cancel</Button>
                        <Button variant="destructive" onClick={handleDelete} disabled={deleteSegment.isPending}>
                            {deleteSegment.isPending ? 'Deleting...' : 'Delete'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </Card>
    );
}
