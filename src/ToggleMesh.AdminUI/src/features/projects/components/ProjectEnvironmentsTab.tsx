import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Box, ArrowRightLeft, FileClock, Settings, Key, GripVertical } from 'lucide-react';
import { useCreateEnvironment, useCloneEnvironment, useReorderEnvironments } from '@/api/queries';
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
import { ProjectRole, type Environment, type ProjectDetails } from '@/api/types';
import { toast } from 'sonner';
import { Skeleton } from '@/components/ui/skeleton';
import { getEnvBadgeStyle } from '@/utils/styleHelpers';
import { AuditLogViewer } from '@/features/audit/components/AuditLogViewer';


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
                                <CardContent className="p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                                    <div className="flex flex-col sm:flex-row sm:items-center gap-4">
                                        {canManageProject && localEnvs.length > 1 && (
                                            <div
                                                className="hidden sm:flex items-center text-muted-foreground/30 group-hover:text-muted-foreground/60 transition-colors sm:pb-0 sm:pr-2 sm:border-r border-border/10 shrink-0">
                                                <GripVertical className="h-5 w-5" />
                                            </div>
                                        )}

                                        <div className="space-y-2 sm:space-y-1.5">
                                            <div className="flex flex-wrap items-center gap-2.5">
                                                <Box className="h-5 w-5 text-muted-foreground shrink-0" />
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

                                    <div className="flex flex-wrap sm:flex-nowrap items-center gap-2 mt-2 sm:mt-0" onClick={(e) => e.stopPropagation()}>
                                        {env.userRole < 3 && (
                                            <Button variant="outline" size="sm" onClick={() => setAuditEnvId(env.id)}
                                                className="flex-1 sm:flex-none h-9 px-3 text-xs font-medium cursor-pointer">
                                                <FileClock className="mr-1.5 h-3.5 w-3.5 shrink-0" />
                                                Logs
                                            </Button>
                                        )}
                                        {canManageEnv && (
                                            <Button variant="outline" size="sm" onClick={() => {
                                                setEnvToSync(env.id);
                                                syncForm.reset({ sourceEnvId: '' });
                                            }} className="flex-1 sm:flex-none h-9 px-3 text-xs font-medium cursor-pointer">
                                                <ArrowRightLeft className="mr-1.5 h-3.5 w-3.5 shrink-0" />
                                                Sync
                                            </Button>
                                        )}
                                        {canManageEnv ? (
                                            <Button variant="default" size="sm"
                                                onClick={() => navigate(`/projects/${project?.id}/environments/${env.id}`)}
                                                className="flex-1 sm:flex-none h-9 px-3 text-xs font-medium cursor-pointer">
                                                <Settings className="mr-1.5 h-3.5 w-3.5 shrink-0" />
                                                Configure
                                            </Button>
                                        ) : (
                                            <Button variant="default" size="sm"
                                                onClick={() => navigate(`/projects/${project?.id}/environments/${env.id}`)}
                                                className="flex-1 sm:flex-none h-9 px-3 text-xs font-medium cursor-pointer">
                                                <Settings className="mr-1.5 h-3.5 w-3.5 shrink-0" />
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
                        {auditEnvId && (
                            <AuditLogViewer
                                environmentId={auditEnvId}
                                pageSize={6}
                                className="h-full flex flex-col justify-between space-y-4"
                                tableContainerClassName="rounded-md border border-border/40 overflow-y-auto flex-grow min-h-0 bg-zinc-950/20"
                            />
                        )}
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