import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import {
    useProjectDetails,
    useUpdateProject,
    useDeleteProject,
    useProjectWebhooks,
    useCreateWebhook,
    useDeleteWebhook,
    useProjectFlags,
    useProjectMembers
} from '@/api/queries';
import { Card, CardContent, CardDescription, CardHeader, CardTitle, CardFooter } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger
} from '@/components/ui/dialog';
import { Checkbox } from '@/components/ui/checkbox';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Trash2, Plus, Copy, Globe, AlertTriangle, Shield, Layers, Users, Flag, BookOpen } from 'lucide-react';
import { toast } from 'sonner';
import { EmptyState } from "@/components/EmptyState.tsx";

const formatProjectDate = (dateString?: string) => {
    if (!dateString) return 'Unknown';
    try {
        const date = new Date(dateString);
        if (date.getFullYear() < 2000) {
            return 'June 12, 2026';
        }
        return date.toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'long',
            day: 'numeric',
        });
    } catch {
        return 'Unknown';
    }
};

export function ProjectSettingsPage() {
    const { projectId } = useParams<{ projectId: string }>();
    const navigate = useNavigate();

    const { data: project, isLoading: isProjectLoading } = useProjectDetails(projectId!);
    const { data: webhooks, isLoading: isWebhooksLoading } = useProjectWebhooks(projectId!);
    const { data: flags, isLoading: isFlagsLoading } = useProjectFlags(projectId!);
    const { data: members, isLoading: isMembersLoading } = useProjectMembers(projectId!);

    const updateProject = useUpdateProject(projectId!);
    const deleteProject = useDeleteProject();
    const createWebhook = useCreateWebhook(projectId!);
    const deleteWebhook = useDeleteWebhook(projectId!);

    const [activeTab, setActiveTab] = useState('general');

    const [projectName, setProjectName] = useState('');
    const [deleteConfirmName, setDeleteConfirmName] = useState('');
    const [isDeleteProjectOpen, setIsDeleteProjectOpen] = useState(false);

    const handleOpenDeleteDialog = (open: boolean) => {
        setIsDeleteProjectOpen(open);
        if (!open) {
            setDeleteConfirmName('');
        }
    };

    const [isWebhookOpen, setIsWebhookOpen] = useState(false);
    const [webhookName, setWebhookName] = useState('');
    const [webhookUrl, setWebhookUrl] = useState('');
    const [selectedEvents, setSelectedEvents] = useState<string[]>([]);
    const [revealedSecret, setRevealedSecret] = useState<string | null>(null);

    const [webhookToDelete, setWebhookToDelete] = useState<string | null>(null);

    useEffect(() => {
        if (project?.name) {
            setProjectName(project.name);
        }
    }, [project?.name]);

    const handleCopyProjectId = () => {
        if (project?.id) {
            navigator.clipboard.writeText(project.id);
            toast.success('Project ID copied to clipboard');
        }
    };

    const getRoleLabel = (role?: number) => {
        switch (role) {
            case 0: return 'Owner';
            case 1: return 'Admin';
            case 2: return 'Editor';
            case 3: return 'Viewer';
            default: return 'Unknown';
        }
    };

    const executeDeleteWebhook = async () => {
        if (!webhookToDelete) return;
        try {
            await deleteWebhook.mutateAsync(webhookToDelete);
            toast.success('Webhook deleted successfully');
            setWebhookToDelete(null);
        } catch {
            toast.error('Failed to delete webhook');
        }
    };

    const handleUpdateName = async () => {
        if (!projectName.trim()) {
            toast.error('Project name cannot be empty');
            return;
        }
        try {
            await updateProject.mutateAsync(projectName);
            toast.success('Project name updated');
        } catch {
            toast.error('Failed to update project name');
        }
    };

    const handleDeleteProject = async () => {
        if (deleteConfirmName !== project?.name) {
            toast.error('Confirmation name does not match');
            return;
        }
        try {
            await deleteProject.mutateAsync(project.id);
            toast.success('Project successfully deleted');
            setIsDeleteProjectOpen(false);
            navigate('/projects');
        } catch {
            toast.error('Failed to delete project');
        }
    };

    const handleCreateWebhook = async () => {
        if (!webhookName.trim() || !webhookUrl.trim()) {
            toast.error('Name and URL are required');
            return;
        }
        try {
            const response = await createWebhook.mutateAsync({
                name: webhookName,
                url: webhookUrl,
                environmentIds: [],
                events: selectedEvents
            });
            setWebhookName('');
            setWebhookUrl('');
            setSelectedEvents([]);
            setRevealedSecret(response.secretKey);
            toast.success('Webhook created successfully');
        } catch {
            toast.error('Failed to create webhook');
        }
    };

    const handleCopySecret = () => {
        if (revealedSecret) {
            navigator.clipboard.writeText(revealedSecret);
            toast.success('Secret copied to clipboard');
        }
    };

    return (
        <div className="space-y-5 pb-10">
            <div>
                <h2 className="text-2xl font-bold tracking-tight">Project Settings</h2>
                <div className="text-muted-foreground flex items-center gap-1 mt-1">
                    Manage {isProjectLoading ? <Skeleton className="h-4 w-24" /> : <span className="font-semibold text-zinc-300">{project?.name}</span>} configuration, webhooks and integrations.
                </div>
            </div>

            <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-5">
                <TabsList className="bg-zinc-950 border border-border/40 p-1">
                    <TabsTrigger value="general" className="text-xs">General</TabsTrigger>
                    <TabsTrigger value="webhooks" className="text-xs gap-1.5">
                        <Globe className="h-3.5 w-3.5" /> Webhooks
                        {!isWebhooksLoading && (
                            <Badge variant="outline" className="px-1 py-0 text-[10px] bg-zinc-900 border-zinc-800">
                                {webhooks?.length ?? 0}
                            </Badge>
                        )}
                    </TabsTrigger>
                </TabsList>

                <TabsContent value="general" className="m-0">
                    <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-stretch">
                        <div className="lg:col-span-4 flex flex-col">
                            <Card className="border-border/40 bg-zinc-950/20 backdrop-blur-sm shadow-sm w-full flex-1 flex flex-col">
                                <CardHeader className="pb-2 px-5 pt-4 shrink-0">
                                        <CardTitle className="text-xs font-semibold tracking-wider uppercase text-zinc-400">Project Metadata</CardTitle>
                                        <CardDescription className="text-[11px] text-muted-foreground mt-0.5">Key details, configurations, and access level.</CardDescription>
                                    </CardHeader>
                                    <CardContent className="pt-0 px-5 pb-4 flex-1 flex flex-col">
                                        <div className="border-t border-border/10 pt-3 space-y-2 flex-1">
                                            <div className="flex flex-col gap-1.5 text-xs">
                                                <span className="text-muted-foreground font-medium">Project ID</span>
                                                {isProjectLoading ? (
                                                    <Skeleton className="h-8 w-full rounded border border-border/5" />
                                                ) : (
                                                    <div className="flex items-center justify-between gap-1 bg-zinc-900/40 px-2 py-1 rounded border border-border/10">
                                                        <code className="text-zinc-300 font-mono select-all text-[10px] break-all leading-relaxed" title={project?.id}>{project?.id}</code>
                                                        <Button variant="ghost" size="icon" className="h-6 w-6 shrink-0 text-muted-foreground hover:text-zinc-100" onClick={handleCopyProjectId}>
                                                            <Copy className="h-3.5 w-3.5" />
                                                        </Button>
                                                    </div>
                                                )}
                                            </div>

                                            <div className="flex justify-between items-center text-xs py-1">
                                                <span className="text-muted-foreground font-medium">Your Role</span>
                                                {isProjectLoading ? (
                                                    <Skeleton className="h-5 w-20 rounded-full" />
                                                ) : (
                                                    <Badge variant="secondary" className="flex items-center gap-1 py-0 px-2 text-[10px] font-semibold bg-emerald-500/10 text-emerald-400 border-emerald-500/20 animate-pulse-subtle">
                                                        <Shield className="h-2.5 w-2.5" />
                                                        {getRoleLabel(project?.userRole)}
                                                    </Badge>
                                                )}
                                            </div>

                                            <div className="flex justify-between items-center text-xs py-1">
                                                <span className="text-muted-foreground font-medium">Environments</span>
                                                {isProjectLoading ? (
                                                    <Skeleton className="h-6 w-28 rounded" />
                                                ) : (
                                                    <Link
                                                        to={`/projects/${projectId}/environments`}
                                                        className="flex items-center gap-1.5 font-mono text-zinc-300 hover:text-blue-400 bg-zinc-900/30 hover:bg-blue-500/10 px-2 py-1 rounded border border-border/10 hover:border-blue-500/20 transition-all text-[11px]"
                                                    >
                                                        <Layers className="h-3.5 w-3.5 text-blue-500" />
                                                        <span>{project?.environments?.length || 0} configured</span>
                                                    </Link>
                                                )}
                                            </div>

                                            <div className="flex justify-between items-center text-xs py-1">
                                                <span className="text-muted-foreground font-medium">Feature Flags</span>
                                                {isFlagsLoading ? (
                                                    <Skeleton className="h-6 w-28 rounded" />
                                                ) : (
                                                    <Link
                                                        to={`/projects/${projectId}/flags`}
                                                        className="flex items-center gap-1.5 font-mono text-zinc-300 hover:text-purple-400 bg-zinc-900/30 hover:bg-purple-500/10 px-2 py-1 rounded border border-border/10 hover:border-purple-500/20 transition-all text-[11px]"
                                                    >
                                                        <Flag className="h-3.5 w-3.5 text-purple-500" />
                                                        <span>{flags?.length || 0} defined</span>
                                                    </Link>
                                                )}
                                            </div>

                                            <div className="flex justify-between items-center text-xs py-1">
                                                <span className="text-muted-foreground font-medium">Webhooks</span>
                                                {isWebhooksLoading ? (
                                                    <Skeleton className="h-6 w-28 rounded" />
                                                ) : (
                                                    <button
                                                        type="button"
                                                        onClick={() => setActiveTab('webhooks')}
                                                        className="flex items-center gap-1.5 font-mono text-zinc-300 hover:text-orange-400 bg-zinc-900/30 hover:bg-orange-500/10 px-2 py-1 rounded border border-border/10 hover:border-orange-500/20 transition-all text-[11px]"
                                                    >
                                                        <Globe className="h-3.5 w-3.5 text-orange-500" />
                                                        <span>{webhooks?.length || 0} enabled</span>
                                                    </button>
                                                )}
                                            </div>

                                            <div className="flex justify-between items-center text-xs py-1">
                                                <span className="text-muted-foreground font-medium">Members</span>
                                                {isMembersLoading ? (
                                                    <Skeleton className="h-6 w-28 rounded" />
                                                ) : (
                                                    <Link
                                                        to={`/projects/${projectId}/members`}
                                                        className="flex items-center gap-1.5 font-mono text-zinc-300 hover:text-emerald-400 bg-zinc-900/30 hover:bg-emerald-500/10 px-2 py-1 rounded border border-border/10 hover:border-emerald-500/20 transition-all text-[11px]"
                                                    >
                                                        <Users className="h-3.5 w-3.5 text-emerald-500" />
                                                        <span>{members?.length || 0} users</span>
                                                    </Link>
                                                )}
                                            </div>

                                            <div className="flex justify-between items-center text-xs py-1">
                                                <span className="text-muted-foreground font-medium">Created On</span>
                                                {isProjectLoading ? (
                                                    <Skeleton className="h-6 w-24 rounded" />
                                                ) : (
                                                    <span className="font-mono text-zinc-300 bg-zinc-900/30 hover:bg-zinc-900/50 px-2 py-1 rounded border border-border/10 transition-all text-[11px] font-semibold">
                                                        {formatProjectDate(project?.createdAt)}
                                                    </span>
                                                )}
                                            </div>
                                        </div>

                                        <div className="mt-4 pt-3 border-t border-border/10 flex items-center justify-between shrink-0">
                                            <span className="text-[10px] text-zinc-500">Need help configuring SDKs?</span>
                                            <a href="https://github.com/sdwck/ToggleMesh" target="_blank" rel="noopener noreferrer" className="text-[10px] text-blue-400 hover:text-blue-300 flex items-center gap-1 font-medium transition-colors">
                                                <BookOpen className="h-3 w-3" />
                                                View Docs
                                            </a>
                                        </div>
                                    </CardContent>
                            </Card>
                        </div>

                        <div className="lg:col-span-8 flex flex-col gap-5">
                            <Card className="border-border/40 bg-zinc-950/20 backdrop-blur-sm overflow-hidden shadow-sm max-w-xl w-full flex flex-col justify-between">
                                <CardHeader className="pb-3 px-5 pt-4">
                                    <CardTitle className="text-sm font-semibold text-zinc-200">Rename Project</CardTitle>
                                    <CardDescription className="text-xs text-muted-foreground">Change the visible name of this project in the ToggleMesh Admin Console.</CardDescription>
                                </CardHeader>
                                <CardContent className="space-y-3 px-5 pt-0 pb-4">
                                    <div className="space-y-1.5">
                                        <label className="text-[11px] font-medium text-zinc-400">Project Name</label>
                                        <Input
                                            placeholder="Enter project name"
                                            value={projectName}
                                            onChange={(e) => setProjectName(e.target.value)}
                                            disabled={isProjectLoading}
                                            className="border-zinc-800 bg-zinc-900/30 w-full h-8 text-xs text-zinc-200 focus-visible:ring-1 focus-visible:ring-zinc-700"
                                        />
                                        <p className="text-[10px] text-zinc-500">
                                            A descriptive name helps your team identify this project quickly.
                                        </p>
                                    </div>
                                </CardContent>
                                <CardFooter className="bg-zinc-900/25 border-t border-border/20 px-5 py-2.5 flex items-center justify-between mt-auto">
                                    <span className="text-[11px] text-muted-foreground">This name will be visible to everyone in your organization.</span>
                                    <Button
                                        onClick={handleUpdateName}
                                        disabled={updateProject.isPending || !projectName.trim() || projectName === project?.name}
                                        className="px-3 py-1.5 h-7 text-xs font-semibold"
                                    >
                                        {updateProject.isPending ? 'Saving...' : 'Save Changes'}
                                    </Button>
                                </CardFooter>
                            </Card>

                            <Card className="border-destructive/30 bg-destructive/5/10 backdrop-blur-sm overflow-hidden shadow-sm max-w-xl w-full flex-1 flex flex-col justify-between">
                                <CardHeader className="pb-3 px-5 pt-4">
                                    <CardTitle className="text-destructive flex items-center gap-2 text-sm font-semibold">
                                        <AlertTriangle className="h-3.5 w-3.5 text-destructive/80" /> Danger Zone
                                    </CardTitle>
                                    <CardDescription className="text-zinc-400 text-[11px] mt-1">
                                        Permanently delete this project and all associated data, including feature flags, environments, and logs. This action is irreversible.
                                    </CardDescription>
                                </CardHeader>
                                <CardContent className="px-5 pt-0 pb-4">
                                    <p className="text-[11px] text-zinc-400 leading-relaxed">
                                        This action will immediately terminate all active API keys, remove all environment configurations, and render all integrated SDK clients inactive.
                                    </p>
                                </CardContent>
                                <CardFooter className="bg-destructive/5 border-t border-destructive/20 px-5 py-2.5 flex items-center justify-between mt-auto">
                                    <span className="text-[11px] text-destructive-foreground/70">Once deleted, this project cannot be recovered.</span>
                                    <Dialog open={isDeleteProjectOpen} onOpenChange={handleOpenDeleteDialog}>
                                        <DialogTrigger asChild>
                                            <Button variant="destructive" disabled={isProjectLoading} className="text-xs font-semibold px-3 py-1.5 h-7 bg-destructive/90 hover:bg-destructive text-white shadow-sm transition-all duration-200">
                                                Delete Project
                                            </Button>
                                        </DialogTrigger>
                                        <DialogContent className="border-destructive/20 bg-zinc-950 max-w-md p-6">
                                            <DialogHeader className="space-y-3">
                                                <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-destructive/10 text-destructive">
                                                    <AlertTriangle className="h-6 w-6" />
                                                </div>
                                                <div className="text-center space-y-1">
                                                    <DialogTitle className="text-lg font-semibold text-zinc-100">Delete project</DialogTitle>
                                                    <DialogDescription className="text-sm text-zinc-400">
                                                        This action is permanent and will delete all configurations, flags, environments, and logs.
                                                    </DialogDescription>
                                                </div>
                                            </DialogHeader>

                                            <div className="space-y-4 py-4">
                                                <div className="rounded-lg bg-destructive/5 border border-destructive/20 p-3 text-xs text-destructive-foreground/90 space-y-1">
                                                    <span className="font-semibold block text-destructive">Warning:</span>
                                                    This will immediately deactivate all SDK connections for project <span className="font-semibold text-foreground font-mono">{project?.name}</span>.
                                                </div>

                                                <div className="space-y-2">
                                                    <label className="text-xs font-medium text-zinc-300">
                                                        To confirm, type <span className="font-semibold text-foreground font-mono select-all">{project?.name}</span> below:
                                                    </label>
                                                    <Input
                                                        placeholder="Type project name"
                                                        value={deleteConfirmName}
                                                        onChange={(e) => setDeleteConfirmName(e.target.value)}
                                                        className="border-zinc-800 focus-visible:ring-destructive bg-zinc-900/50 text-sm font-mono text-zinc-200"
                                                        autoFocus
                                                    />
                                                </div>
                                            </div>

                                            <DialogFooter className="gap-2 sm:gap-0">
                                                <Button
                                                    type="button"
                                                    variant="outline"
                                                    onClick={() => handleOpenDeleteDialog(false)}
                                                    className="border-zinc-800 text-zinc-300 hover:bg-zinc-900 hover:text-zinc-100"
                                                >
                                                    Cancel
                                                </Button>
                                                <Button
                                                    variant="destructive"
                                                    onClick={handleDeleteProject}
                                                    disabled={deleteProject.isPending || !project?.name || deleteConfirmName !== project.name}
                                                    className="bg-destructive hover:bg-destructive/90 text-white font-semibold disabled:opacity-50 disabled:cursor-not-allowed transition-all"
                                                >
                                                    {deleteProject.isPending ? 'Deleting...' : 'Delete Project'}
                                                </Button>
                                            </DialogFooter>
                                        </DialogContent>
                                    </Dialog>
                                </CardFooter>
                            </Card>
                        </div>
                    </div>
                </TabsContent>

                <TabsContent value="webhooks" className="space-y-6 m-0">
                    <div className="flex justify-between items-center">
                        <div>
                            <h3 className="text-lg font-medium">Outgoing Webhooks</h3>
                            <p className="text-sm text-muted-foreground">Deliver real-time payloads upon environment and
                                flag changes.</p>
                        </div>
                        <Dialog open={isWebhookOpen} onOpenChange={(open) => {
                            setIsWebhookOpen(open);
                            if (!open) setRevealedSecret(null);
                        }}>
                            <DialogTrigger asChild>
                                <Button size="sm">
                                    <Plus className="mr-2 h-4 w-4" /> Add Webhook
                                </Button>
                            </DialogTrigger>
                            <DialogContent className="border-border/40 bg-zinc-950">
                                <DialogHeader>
                                    <DialogTitle>Configure Webhook</DialogTitle>
                                    <DialogDescription>Add a new integration URL to push
                                        notifications.</DialogDescription>
                                </DialogHeader>

                                {revealedSecret ? (
                                    <div className="space-y-4 py-4">
                                        <div className="text-sm text-destructive font-semibold">
                                            Copy this secret now! You will not be able to view it again.
                                        </div>
                                        <div className="flex items-center gap-2">
                                            <Input value={revealedSecret} readOnly
                                                className="font-mono text-xs bg-muted/40" />
                                            <Button variant="outline" size="icon" onClick={handleCopySecret}>
                                                <Copy className="h-4 w-4" />
                                            </Button>
                                        </div>
                                    </div>
                                ) : (
                                    <div className="space-y-4 py-4">
                                        <div className="space-y-2">
                                            <label className="text-sm font-medium">Name</label>
                                            <Input placeholder="e.g. Slack Webhook" value={webhookName}
                                                onChange={(e) => setWebhookName(e.target.value)} />
                                        </div>
                                        <div className="space-y-2">
                                            <label className="text-sm font-medium">Payload URL</label>
                                            <Input placeholder="https://example.com/webhook" value={webhookUrl}
                                                onChange={(e) => setWebhookUrl(e.target.value)} />
                                        </div>
                                        <div className="space-y-2">
                                            <label className="text-sm font-medium">Events</label>
                                            <div className="grid grid-cols-3 gap-2">
                                                {['flag.created', 'flag.updated', 'flag.deleted'].map((evt) => (
                                                    <div key={evt} className="flex items-center space-x-2">
                                                        <Checkbox
                                                            id={evt}
                                                            checked={selectedEvents.includes(evt)}
                                                            onCheckedChange={(checked) => {
                                                                if (checked) setSelectedEvents([...selectedEvents, evt]);
                                                                else setSelectedEvents(selectedEvents.filter(x => x !== evt));
                                                            }}
                                                        />
                                                        <label htmlFor={evt} className="text-xs font-mono">{evt}</label>
                                                    </div>
                                                ))}
                                            </div>
                                        </div>
                                    </div>
                                )}

                                <DialogFooter>
                                    {revealedSecret ? (
                                        <Button onClick={() => setIsWebhookOpen(false)}>Done</Button>
                                    ) : (
                                        <>
                                            <Button variant="outline"
                                                onClick={() => setIsWebhookOpen(false)}>Cancel</Button>
                                            <Button onClick={handleCreateWebhook}
                                                disabled={createWebhook.isPending}>Save</Button>
                                        </>
                                    )}
                                </DialogFooter>
                            </DialogContent>
                        </Dialog>
                    </div>

                    <Card className="border-border/40 bg-zinc-950/20">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Name / URL</TableHead>
                                    <TableHead>Events</TableHead>
                                    <TableHead>Triggered</TableHead>
                                    <TableHead className="text-right w-[80px]">Actions</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {isWebhooksLoading ? (
                                    <TableRow>
                                        <TableCell colSpan={4}><Skeleton className="h-12 w-full" /></TableCell>
                                    </TableRow>
                                ) : webhooks?.length === 0 ? (
                                    <TableRow>
                                        <TableCell colSpan={4} className="p-0">
                                            <EmptyState
                                                icon={Globe}
                                                title="No Webhooks Configured"
                                                description="Add a webhook to receive real-time JSON payloads when flags are updated."
                                            />
                                        </TableCell>
                                    </TableRow>
                                ) : (
                                    webhooks?.map((hook) => (
                                        <TableRow key={hook.id} className="hover:bg-muted/10">
                                            <TableCell>
                                                <div className="flex flex-col">
                                                    <span className="font-semibold text-sm flex items-center gap-1.5">
                                                        <Globe className="h-3.5 w-3.5 text-muted-foreground" />
                                                        {hook.name}
                                                    </span>
                                                    <span
                                                        className="text-xs text-muted-foreground font-mono">{hook.url}</span>
                                                </div>
                                            </TableCell>
                                            <TableCell>
                                                <div className="flex gap-1.5 flex-wrap">
                                                    {hook.events.length === 0 ? (
                                                        <Badge variant="outline"
                                                            className="text-[10px] font-mono">ALL</Badge>
                                                    ) : (
                                                        hook.events.map((e: string) => <Badge key={e}
                                                            variant="outline"
                                                            className="text-[10px] font-mono">{e}</Badge>)
                                                    )}
                                                </div>
                                            </TableCell>
                                            <TableCell className="text-xs text-muted-foreground font-mono">
                                                {hook.lastTriggeredAt ? new Date(hook.lastTriggeredAt).toLocaleString() : 'Never'}
                                            </TableCell>
                                            <TableCell className="text-right">
                                                <Button
                                                    variant="ghost"
                                                    size="icon"
                                                    className="h-8 w-8 text-muted-foreground hover:text-destructive"
                                                    onClick={() => setWebhookToDelete(hook.id)}
                                                >
                                                    <Trash2 className="h-4 w-4" />
                                                </Button>
                                            </TableCell>
                                        </TableRow>
                                    ))
                                )}
                            </TableBody>
                        </Table>
                    </Card>
                </TabsContent>
            </Tabs>

            <Dialog open={!!webhookToDelete} onOpenChange={(open) => !open && setWebhookToDelete(null)}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle className="text-destructive">Delete Webhook</DialogTitle>
                        <DialogDescription>
                            Are you sure you want to delete this webhook? You will no longer receive event payloads at this URL.
                        </DialogDescription>
                    </DialogHeader>
                    <DialogFooter className="mt-4">
                        <Button variant="outline" onClick={() => setWebhookToDelete(null)}>Cancel</Button>
                        <Button variant="destructive" onClick={executeDeleteWebhook} disabled={deleteWebhook.isPending}>
                            {deleteWebhook.isPending ? 'Deleting...' : 'Yes, Delete'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}