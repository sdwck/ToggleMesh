import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, FolderGit2, AlertTriangle, ArrowRight, TerminalSquare, FlaskConical, Sparkles } from 'lucide-react';
import { useQueryClient } from '@tanstack/react-query';
import api from '@/api/axios';
import { useProjects, useCreateProject, useOrganizations, useCreateOrganization, useSystemConfig } from '@/api/queries';
import { useOrganizationStore } from '@/stores/useOrganizationStore';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem, SelectSeparator } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { toast } from 'sonner';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import {
    Form,
    FormControl,
    FormField,
    FormItem,
    FormLabel,
    FormMessage,
} from '@/components/ui/form';
import { handleApiError } from '@/api/errorUtils';

const formatEvals = (n: number): string => {
    if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
    if (n >= 1_000) return `${(n / 1_000).toFixed(1)}k`;
    return String(n);
};

const roleLabel = (role: number): string => {
    switch (role) {
        case 0: return 'Owner';
        case 1: return 'Admin';
        case 2: return 'Editor';
        case 3: return 'Viewer';
        default: return 'Member';
    }
};

const createProjectSchema = z.object({
    name: z.string().min(1, 'Project name is required'),
    organizationId: z.string().min(1, 'Organization is required'),
});
type CreateProjectValues = z.infer<typeof createProjectSchema>;

const createOrgSchema = z.object({
    name: z.string().min(1, 'Organization name is required'),
});
type CreateOrgValues = z.infer<typeof createOrgSchema>;

export function ProjectsPage() {
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const { activeOrganizationId, setActiveOrganizationId } = useOrganizationStore();
    const { data: projects, isLoading } = useProjects(activeOrganizationId);
    const { data: organizations, isLoading: isLoadingOrgs } = useOrganizations();
    const createOrganization = useCreateOrganization();
    const createProject = useCreateProject();
    const { data: systemConfig } = useSystemConfig();

    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [isCreateOrgOpen, setIsCreateOrgOpen] = useState(false);

    const projectForm = useForm<CreateProjectValues>({
        resolver: zodResolver(createProjectSchema),
        defaultValues: {
            name: '',
            organizationId: '',
        },
    });

    const orgForm = useForm<CreateOrgValues>({
        resolver: zodResolver(createOrgSchema),
        defaultValues: {
            name: '',
        },
    });

    const handlePrefetchProject = (projId: string) => {
        queryClient.prefetchQuery({
            queryKey: ['projects', projId],
            queryFn: async () => {
                const { data } = await api.get(`/projects/${projId}`);
                return data;
            },
            staleTime: 5 * 60 * 1000,
        });
        queryClient.prefetchQuery({
            queryKey: ['projects', projId, 'flags', undefined, undefined],
            queryFn: async () => {
                const { data } = await api.get(`/projects/${projId}/flags`);
                return data;
            },
            staleTime: 5 * 60 * 1000,
        });
    };

    useEffect(() => {
        if (isDialogOpen) {
            let defaultOrgId = '';
            if (organizations && organizations.length > 0) {
                if (activeOrganizationId && organizations.some(o => o.id === activeOrganizationId)) {
                    defaultOrgId = activeOrganizationId;
                } else {
                    defaultOrgId = organizations[0].id;
                }
            }
            projectForm.reset({
                name: '',
                organizationId: defaultOrgId,
            });
        }
    }, [isDialogOpen, activeOrganizationId, organizations, projectForm]);

    useEffect(() => {
        if (isCreateOrgOpen) {
            orgForm.reset({ name: '' });
        }
    }, [isCreateOrgOpen, orgForm]);

    const handleCreateProject = async (values: CreateProjectValues) => {
        try {
            const newProject = await createProject.mutateAsync(values);
            setIsDialogOpen(false);
            toast.success('Project created');
            if (newProject?.id) {
                navigate(`/projects/${newProject.id}`);
            }
        } catch (error: any) {
            handleApiError(error, projectForm.setError, 'Failed to create project');
        }
    };

    const handleCreateOrg = async (values: CreateOrgValues) => {
        try {
            const newOrg = await createOrganization.mutateAsync(values);
            setIsCreateOrgOpen(false);
            toast.success('Organization created');
            if (newOrg?.id) {
                setActiveOrganizationId(newOrg.id);
                projectForm.setValue('organizationId', newOrg.id);
            }
        } catch (error: any) {
            handleApiError(error, orgForm.setError, 'Failed to create organization');
        }
    };

    return (
        <div className="p-8 max-w-[1400px] mx-auto w-full">
            <div className="flex items-center justify-between mb-8">
                <div>
                    <h1 className="text-2xl font-bold tracking-tight">Projects</h1>
                    <p className="text-muted-foreground text-sm mt-1">Manage your workspace projects and environments.</p>
                </div>

                <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
                    <DialogTrigger asChild>
                        <Button className="cursor-pointer">
                            <Plus className="mr-2 h-4 w-4" />
                            New Project
                        </Button>
                    </DialogTrigger>
                    <DialogContent className="border-border/40 bg-zinc-950">
                        <DialogHeader>
                            <DialogTitle>Create Project</DialogTitle>
                            <DialogDescription>
                                A project represents a single application or microservice.
                            </DialogDescription>
                        </DialogHeader>
                        <Form {...projectForm}>
                            <form onSubmit={projectForm.handleSubmit(handleCreateProject)} className="space-y-4 py-4 text-left">
                                <FormField
                                    control={projectForm.control}
                                    name="name"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Project Name</FormLabel>
                                            <FormControl>
                                                <Input
                                                    placeholder="e.g., e-commerce-api"
                                                    {...field}
                                                    autoFocus
                                                />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />

                                <FormField
                                    control={projectForm.control}
                                    name="organizationId"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Organization</FormLabel>
                                            <Select value={field.value} onValueChange={(val) => {
                                                if (val === "__new__") {
                                                    setIsCreateOrgOpen(true);
                                                } else {
                                                    field.onChange(val);
                                                }
                                            }}>
                                                <FormControl>
                                                    <SelectTrigger className="border-border/40 bg-zinc-950/50">
                                                        <SelectValue placeholder={isLoadingOrgs ? "Loading organizations..." : "Select organization"} />
                                                    </SelectTrigger>
                                                </FormControl>
                                                <SelectContent className="border-border/40 bg-zinc-950/95 backdrop-blur-xl">
                                                    {organizations?.map((org) => (
                                                        <SelectItem key={org.id} value={org.id}>
                                                            {org.name}
                                                        </SelectItem>
                                                    ))}
                                                    {systemConfig?.allowUserOrganizationCreation === true && (
                                                        <>
                                                            {organizations && organizations.length > 0 && <SelectSeparator className="bg-border/40" />}
                                                            <SelectItem value="__new__" className="text-primary focus:text-primary font-medium">
                                                                + Create new organization...
                                                            </SelectItem>
                                                        </>
                                                    )}
                                                </SelectContent>
                                            </Select>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />

                                {projectForm.formState.errors.root && (
                                    <div className="text-sm text-destructive font-medium">
                                        {projectForm.formState.errors.root.message}
                                    </div>
                                )}

                                <DialogFooter className="mt-4">
                                    <Button type="button" variant="outline" onClick={() => setIsDialogOpen(false)}>Cancel</Button>
                                    <Button type="submit" disabled={createProject.isPending}>
                                        {createProject.isPending ? 'Creating...' : 'Create'}
                                    </Button>
                                </DialogFooter>
                            </form>
                        </Form>
                    </DialogContent>
                </Dialog>
            </div>

            <Dialog open={isCreateOrgOpen} onOpenChange={setIsCreateOrgOpen}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle>Create Organization</DialogTitle>
                        <DialogDescription>
                            An organization contains your projects and team members.
                        </DialogDescription>
                    </DialogHeader>
                    <Form {...orgForm}>
                        <form onSubmit={orgForm.handleSubmit(handleCreateOrg)} className="space-y-4 py-4">
                            <FormField
                                control={orgForm.control}
                                name="name"
                                render={({ field }) => (
                                    <FormItem>
                                        <FormLabel>Organization Name</FormLabel>
                                        <FormControl>
                                            <Input
                                                placeholder="e.g., Acme Corp"
                                                {...field}
                                                autoFocus
                                            />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />

                            {orgForm.formState.errors.root && (
                                <div className="text-sm text-destructive font-medium">
                                    {orgForm.formState.errors.root.message}
                                </div>
                            )}

                            <DialogFooter>
                                <Button type="button" variant="outline" onClick={() => setIsCreateOrgOpen(false)}>Cancel</Button>
                                <Button type="submit" disabled={createOrganization.isPending}>
                                    {createOrganization.isPending ? 'Creating...' : 'Create'}
                                </Button>
                            </DialogFooter>
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>
            {isLoading ? (
                <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
                    {[...Array(6)].map((_, i) => (
                        <Card key={i} className="border-border/40 h-full">
                            <CardContent className="p-5 h-full flex flex-col space-y-4">
                                <div className="flex items-center gap-3">
                                    <Skeleton className="h-10 w-10 rounded-xl" />
                                    <div className="space-y-1.5 flex-1">
                                        <Skeleton className="h-4 w-32 rounded" />
                                        <Skeleton className="h-3 w-16 rounded" />
                                    </div>
                                </div>
                                <div className="grid grid-cols-2 gap-3 mt-4">
                                    <Skeleton className="h-14 w-full rounded-xl" />
                                    <Skeleton className="h-14 w-full rounded-xl" />
                                </div>
                            </CardContent>
                        </Card>
                    ))}
                </div>
            ) : projects?.length === 0 ? (
                <Card className="border-border/40 border-dashed p-12 text-center">
                    <FolderGit2 className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                    <h3 className="font-semibold text-lg">No projects found</h3>
                    <p className="text-muted-foreground text-sm mb-6">Create your first project to get started.</p>
                    <Button onClick={() => setIsDialogOpen(true)}>Create Project</Button>
                </Card>
            ) : (
                <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
                    {projects?.map((project) => (
                        <Card
                            key={project.id}
                            className="flex flex-col border-border/40 hover:border-primary/40 bg-gradient-to-b from-zinc-950/40 to-zinc-950/10 hover:bg-zinc-900/40 transition-all duration-300 cursor-pointer group overflow-hidden relative shadow-sm hover:shadow-md"
                            onClick={() => navigate(`/projects/${project.id}`)}
                            onMouseEnter={() => handlePrefetchProject(project.id)}
                        >
                            <div className="p-5 flex-1 flex flex-col">
                                <div className="flex items-start justify-between mb-5">
                                    <div className="flex items-center gap-3 min-w-0">
                                        <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-primary/20 to-primary/5 border border-primary/20 flex items-center justify-center text-primary shrink-0 shadow-sm group-hover:scale-105 transition-transform duration-300">
                                            <FolderGit2 className="h-5 w-5" />
                                        </div>
                                        <div className="min-w-0">
                                            <h3 className="font-semibold text-[15px] tracking-tight truncate group-hover:text-primary transition-colors">
                                                {project.name}
                                            </h3>
                                            <span className="text-[12px] text-muted-foreground/70 font-medium block mt-0.5">
                                                {roleLabel(project.userRole)}
                                            </span>
                                        </div>
                                    </div>
                                    <ArrowRight className="h-4 w-4 text-primary opacity-0 -translate-x-2 group-hover:opacity-100 group-hover:translate-x-0 transition-all duration-300 shrink-0 mt-2" />
                                </div>

                                {project.totalFlags === 0 && !project.evaluations24H ? (
                                    <div className="flex-1 flex flex-col items-center justify-center text-muted-foreground/40 py-6 border border-dashed border-border/20 rounded-xl bg-zinc-950/30">
                                        <TerminalSquare className="h-6 w-6 mb-2 opacity-30" />
                                        <span className="text-[13px] font-medium text-center px-4">
                                            {project.userRole <= 2 ? 'Ready for setup. Create your first flag.' : 'No flags yet'}
                                        </span>
                                    </div>
                                ) : (
                                    <div className="flex flex-col flex-1 justify-between gap-5">
                                        <div className="grid grid-cols-2 gap-3">
                                            <div className="flex flex-col gap-1.5 p-3 rounded-xl bg-zinc-900/30 border border-border/20">
                                                <span className="text-[10px] text-muted-foreground font-semibold uppercase tracking-wider">Flags</span>
                                                <div className="flex items-baseline gap-1.5">
                                                    <span className="text-xl font-bold text-zinc-200">{project.activeFlags}</span>
                                                    <span className="text-[12px] text-zinc-500 font-medium">/ {project.totalFlags}</span>
                                                </div>
                                            </div>
                                            <div className="flex flex-col gap-1.5 p-3 rounded-xl bg-zinc-900/30 border border-border/20">
                                                <span className="text-[10px] text-muted-foreground font-semibold uppercase tracking-wider">Requests (24h)</span>
                                                {project.evaluations24H > 0 ? (
                                                    <span className="text-xl font-bold text-sky-400">{formatEvals(project.evaluations24H)}</span>
                                                ) : (
                                                    <span className="text-xl font-bold text-zinc-600">0</span>
                                                )}
                                            </div>
                                        </div>

                                        <div className="flex flex-col gap-2.5 mt-auto">
                                            {(project.runningExperiments > 0 || project.mabActiveFlagsCount > 0) && (
                                                <div className="flex items-center gap-2 flex-wrap">
                                                    {project.runningExperiments > 0 && (
                                                        <div className="flex items-center gap-1.5 bg-emerald-500/10 text-emerald-400 px-2.5 py-1 rounded-md text-[11px] font-medium border border-emerald-500/15">
                                                            <FlaskConical className="h-3 w-3" />
                                                            <span>{project.runningExperiments} Active Test{project.runningExperiments > 1 ? 's' : ''}</span>
                                                        </div>
                                                    )}
                                                    {project.mabActiveFlagsCount > 0 && (
                                                        <div className="flex items-center gap-1.5 bg-amber-500/10 text-amber-400 px-2.5 py-1 rounded-md text-[11px] font-medium border border-amber-500/20">
                                                            <Sparkles className="h-3 w-3" />
                                                            <span>{project.mabActiveFlagsCount} Auto-Tuning</span>
                                                        </div>
                                                    )}
                                                </div>
                                            )}

                                            <div className="flex flex-col gap-1.5 pt-1">
                                                {project.topExperimentFlagKey && (
                                                    <div className="flex items-center gap-2 text-[12px] text-muted-foreground">
                                                        <FlaskConical className="h-3.5 w-3.5 text-emerald-500/70 shrink-0" />
                                                        <span className="truncate">Top test: <span className="text-zinc-300 font-medium">{project.topExperimentFlagKey}</span></span>
                                                    </div>
                                                )}

                                                {project.failingWebhooksCount > 0 && (
                                                    <div className="flex items-center gap-2 text-[12px] text-rose-400 bg-rose-500/5 px-2 py-1 -ml-2 rounded-md">
                                                        <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
                                                        <span className="font-medium">{project.failingWebhooksCount} webhook{project.failingWebhooksCount > 1 ? 's' : ''} failing</span>
                                                    </div>
                                                )}
                                            </div>
                                        </div>
                                    </div>
                                )}
                            </div>
                        </Card>
                    ))}
                </div>
            )}
        </div>
    );
}