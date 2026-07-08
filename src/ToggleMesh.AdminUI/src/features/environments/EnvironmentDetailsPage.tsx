import { useState } from 'react';
import { useParams, useNavigate, useSearchParams } from 'react-router-dom';
import {
    useProjectDetails,
    useUpdateEnvironment,
    useDeleteEnvironment
} from '@/api/queries';
import { ProjectRole } from '@/api/types';
import { Button } from '@/components/ui/button';
import { ArrowLeft, Edit2, Trash2, MoreHorizontal, AlertTriangle } from 'lucide-react';
import { toast } from 'sonner';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle
} from '@/components/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { EnvironmentSegmentsTab } from './components/EnvironmentSegmentsTab';
import { EnvironmentApiKeysTab } from './components/EnvironmentApiKeysTab';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import { Form, FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { handleApiError } from '@/api/errorUtils';

const renameEnvSchema = z.object({
    name: z.string().min(1, 'Environment name cannot be empty')
});
type RenameEnvValues = z.infer<typeof renameEnvSchema>;

const deleteEnvSchema = z.object({
    confirmName: z.string()
});
type DeleteEnvValues = z.infer<typeof deleteEnvSchema>;

export function EnvironmentDetailsPage() {
    const { projectId, environmentId } = useParams<{ projectId: string; environmentId: string }>();
    const navigate = useNavigate();
    const [searchParams, setSearchParams] = useSearchParams();
    const currentTab = searchParams.get('tab') || 'segments';

    const { data: project, isLoading: isProjectLoading } = useProjectDetails(projectId!);

    const updateEnvironment = useUpdateEnvironment(projectId!, environmentId!);
    const deleteEnvironment = useDeleteEnvironment(projectId!);

    const [isRenameOpen, setIsRenameOpen] = useState(false);
    const [isDeleteOpen, setIsDeleteOpen] = useState(false);

    const canManageKeys = project?.userRole === ProjectRole.Owner || project?.userRole === ProjectRole.Admin;
    const canEditEnv = project?.userRole === ProjectRole.Owner || project?.userRole === ProjectRole.Admin || project?.userRole === ProjectRole.Editor;

    const renameForm = useForm<RenameEnvValues>({
        resolver: zodResolver(renameEnvSchema),
        defaultValues: { name: '' }
    });

    const deleteForm = useForm<DeleteEnvValues>({
        resolver: zodResolver(deleteEnvSchema),
        defaultValues: { confirmName: '' }
    });

    const handleRenameSubmit = async (values: RenameEnvValues) => {
        try {
            await updateEnvironment.mutateAsync(values.name.trim());
            setIsRenameOpen(false);
            toast.success('Environment renamed successfully');
        } catch (error: any) {
            handleApiError(error, renameForm.setError, 'Failed to rename environment');
        }
    };

    const handleDeleteEnvSubmit = async (values: DeleteEnvValues) => {
        const environment = project?.environments?.find(e => e.id === environmentId);
        if (values.confirmName !== environment?.name) {
            deleteForm.setError('confirmName', { message: 'Name does not match' });
            return;
        }

        try {
            await deleteEnvironment.mutateAsync(environmentId!);
            setIsDeleteOpen(false);
            toast.success('Environment deleted successfully');
            navigate(`/projects/${projectId}/environments`);
        } catch (error: any) {
            handleApiError(error, deleteForm.setError, 'Failed to delete environment');
        }
    };

    const environment = project?.environments?.find(e => e.id === environmentId);

    if (!isProjectLoading && (!project || !environment)) {
        return (
            <div className="p-8 text-center text-muted-foreground flex flex-col items-center justify-center min-h-[400px]">
                <AlertTriangle className="h-10 w-10 text-destructive mb-4" />
                <h3 className="font-semibold text-lg text-zinc-200">Environment not found</h3>
                <p className="text-sm mt-1">The environment or project you are trying to access does not exist.</p>
                <Button variant="outline" className="mt-6" onClick={() => navigate('/projects')}>
                    Back to Projects
                </Button>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div className="flex items-center gap-4">
                    <Button variant="ghost" size="icon" onClick={() => navigate(`/projects/${projectId}/environments`)}
                        className="cursor-pointer">
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div>
                        <h1 className="text-3xl font-bold tracking-tight h-9 flex items-center gap-2">
                            {isProjectLoading ? (
                                <Skeleton className="h-8 w-48" />
                            ) : (
                                <>
                                    {environment?.name}
                                    {canManageKeys && (
                                        <DropdownMenu>
                                            <DropdownMenuTrigger asChild>
                                                <Button variant="ghost" size="icon"
                                                    className="h-8 w-8 text-muted-foreground hover:text-foreground cursor-pointer rounded-md mt-1">
                                                    <MoreHorizontal className="h-4 w-4" />
                                                </Button>
                                            </DropdownMenuTrigger>
                                            <DropdownMenuContent align="start"
                                                className="border-border/40 bg-zinc-950 w-44">
                                                <DropdownMenuItem onClick={() => {
                                                    renameForm.reset({ name: environment?.name || '' });
                                                    setIsRenameOpen(true);
                                                }} className="cursor-pointer">
                                                    <Edit2 className="mr-2 h-4 w-4" /> Rename
                                                </DropdownMenuItem>
                                                <DropdownMenuItem onClick={() => {
                                                    deleteForm.reset({ confirmName: '' });
                                                    setIsDeleteOpen(true);
                                                }} className="text-destructive focus:text-destructive cursor-pointer">
                                                    <Trash2 className="mr-2 h-4 w-4" /> Delete
                                                </DropdownMenuItem>
                                            </DropdownMenuContent>
                                        </DropdownMenu>
                                    )}
                                </>
                            )}
                        </h1>
                        <p className="text-muted-foreground">Manage keys and configurations for this environment</p>
                    </div>
                </div>
            </div>

            <Tabs value={currentTab} onValueChange={(val) => setSearchParams({ tab: val })} className="mt-6 space-y-4">
                <TabsList className="bg-zinc-950 border border-border/40">
                    <TabsTrigger value="segments" className="text-xs">Segments</TabsTrigger>
                    {canManageKeys && (
                        <TabsTrigger value="keys" className="text-xs">API Keys</TabsTrigger>
                    )}
                </TabsList>

                {canManageKeys && (
                    <TabsContent value="keys" className="space-y-4">
                        <EnvironmentApiKeysTab projectId={projectId!} environmentId={environmentId!} />
                    </TabsContent>
                )}

                <TabsContent value="segments" className="space-y-4">
                    <EnvironmentSegmentsTab projectId={projectId!} environmentId={environmentId!} canManage={canEditEnv} />
                </TabsContent>
            </Tabs>

            <Dialog open={isRenameOpen} onOpenChange={setIsRenameOpen}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle>Rename Environment</DialogTitle>
                        <DialogDescription>
                            Change the name of this environment. The ID will remain unchanged.
                        </DialogDescription>
                    </DialogHeader>
                    <Form {...renameForm}>
                        <form onSubmit={renameForm.handleSubmit(handleRenameSubmit)}>
                            <div className="space-y-4 py-4">
                                <FormField
                                    control={renameForm.control}
                                    name="name"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormControl>
                                                <Input {...field} placeholder="e.g. Production" autoFocus />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                            </div>
                            <DialogFooter>
                                <Button type="button" variant="outline" onClick={() => setIsRenameOpen(false)}>Cancel</Button>
                                <Button type="submit" disabled={updateEnvironment.isPending}>
                                    {updateEnvironment.isPending ? 'Saving...' : 'Save'}
                                </Button>
                            </DialogFooter>
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>

            <Dialog open={isDeleteOpen} onOpenChange={setIsDeleteOpen}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle className="text-destructive">Delete Environment</DialogTitle>
                        <DialogDescription>
                            This will permanently delete the environment <strong>{environment?.name}</strong> and all associated configurations and API keys. This action cannot be undone.
                        </DialogDescription>
                    </DialogHeader>
                    <Form {...deleteForm}>
                        <form onSubmit={deleteForm.handleSubmit(handleDeleteEnvSubmit)}>
                            <div className="space-y-4 py-4">
                                <p className="text-sm text-muted-foreground">
                                    To confirm deletion, type <strong
                                        className="text-foreground font-mono">{environment?.name}</strong> below:
                                </p>
                                <FormField
                                    control={deleteForm.control}
                                    name="confirmName"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormControl>
                                                <Input
                                                    {...field}
                                                    placeholder="Type environment name to confirm"
                                                    className="border-destructive/30 focus-visible:ring-destructive bg-zinc-950/20"
                                                />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                            </div>
                            <DialogFooter>
                                <Button type="button" variant="outline" onClick={() => setIsDeleteOpen(false)}>Cancel</Button>
                                <Button
                                    type="submit"
                                    variant="destructive"
                                    disabled={deleteEnvironment.isPending || deleteForm.watch('confirmName') !== environment?.name}
                                    className="cursor-pointer"
                                >
                                    Delete Environment
                                </Button>
                            </DialogFooter>
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>
        </div>
    );
}