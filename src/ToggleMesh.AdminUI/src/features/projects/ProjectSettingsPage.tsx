import {useState} from 'react';
import {useParams, useNavigate} from 'react-router-dom';
import {
    useProjectDetails,
    useUpdateProject,
    useDeleteProject,
    useProjectWebhooks,
    useCreateWebhook,
    useDeleteWebhook
} from '@/api/queries';
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import {Table, TableBody, TableCell, TableHead, TableHeader, TableRow} from '@/components/ui/table';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger
} from '@/components/ui/dialog';
import {Checkbox} from '@/components/ui/checkbox';
import {Badge} from '@/components/ui/badge';
import {Skeleton} from '@/components/ui/skeleton';
import {Trash2, Plus, Copy, Globe, AlertTriangle} from 'lucide-react';
import {toast} from 'sonner';
import {EmptyState} from "@/components/EmptyState.tsx";

export function ProjectSettingsPage() {
    const {projectId} = useParams<{ projectId: string }>();
    const navigate = useNavigate();

    const {data: project, isLoading: isProjectLoading} = useProjectDetails(projectId!);
    const {data: webhooks, isLoading: isWebhooksLoading} = useProjectWebhooks(projectId!);

    const updateProject = useUpdateProject(projectId!);
    const deleteProject = useDeleteProject();
    const createWebhook = useCreateWebhook(projectId!);
    const deleteWebhook = useDeleteWebhook(projectId!);

    const [projectName, setProjectName] = useState('');
    const [deleteConfirmName, setDeleteConfirmName] = useState('');

    const [isWebhookOpen, setIsWebhookOpen] = useState(false);
    const [webhookName, setWebhookName] = useState('');
    const [webhookUrl, setWebhookUrl] = useState('');
    const [selectedEvents, setSelectedEvents] = useState<string[]>([]);
    const [revealedSecret, setRevealedSecret] = useState<string | null>(null);

    const [webhookToDelete, setWebhookToDelete] = useState<string | null>(null);

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
        <div className="space-y-6">
            <div>
                <h2 className="text-2xl font-bold tracking-tight">Project Settings</h2>
                <div className="text-muted-foreground flex items-center gap-1 mt-1">
                    Manage {isProjectLoading ? <Skeleton className="h-4 w-24" /> : <span className="font-semibold text-zinc-300">{project?.name}</span>} configuration, webhooks and integrations.
                </div>
            </div>

            <Tabs defaultValue="general" className="space-y-6">
                <TabsList>
                    <TabsTrigger value="general">General</TabsTrigger>
                    <TabsTrigger value="webhooks">Webhooks</TabsTrigger>
                </TabsList>

                <TabsContent value="general" className="space-y-6 m-0">
                    <div className="max-w-md space-y-6">
                        <Card className="border-border/40 bg-zinc-950/20">
                            <CardHeader>
                                <CardTitle>Rename Project</CardTitle>
                                <CardDescription>Change the visible name of this project.</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="flex max-w-md gap-3">
                                    <Input
                                        placeholder="Enter new project name"
                                        value={projectName}
                                        onChange={(e) => setProjectName(e.target.value)}
                                        disabled={isProjectLoading}
                                    />
                                    <Button onClick={handleUpdateName} disabled={updateProject.isPending}>Save</Button>
                                </div>
                            </CardContent>
                        </Card>
                    </div>

                    <div className="max-w-md space-y-6">
                        <Card className="border-destructive/30 bg-destructive/5">
                            <CardHeader>
                                <CardTitle className="text-destructive flex items-center gap-2">
                                    <AlertTriangle className="h-5 w-5"/> Danger Zone
                                </CardTitle>
                                <CardDescription>
                                    Permanently delete this project and all of its associated data. This action is
                                    irreversible.
                                </CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="max-w-md space-y-4">
                                    <p className="text-sm text-muted-foreground flex items-center gap-1 flex-wrap">
                                        To confirm deletion, type
                                        {isProjectLoading ? (
                                            <Skeleton className="h-4 w-24 inline-block" />
                                        ) : (
                                            <strong className="text-foreground font-mono">{project?.name}</strong>
                                        )}
                                        below:
                                    </p>
                                    <Input
                                        placeholder="Type project name to confirm"
                                        value={deleteConfirmName}
                                        onChange={(e) => setDeleteConfirmName(e.target.value)}
                                        className="border-destructive/30 focus-visible:ring-destructive"
                                    />
                                    <Button variant="destructive" onClick={handleDeleteProject}
                                            disabled={deleteProject.isPending}>
                                        Delete Project
                                    </Button>
                                </div>
                            </CardContent>
                        </Card>
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
                                    <Plus className="mr-2 h-4 w-4"/> Add Webhook
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
                                                   className="font-mono text-xs bg-muted/40"/>
                                            <Button variant="outline" size="icon" onClick={handleCopySecret}>
                                                <Copy className="h-4 w-4"/>
                                            </Button>
                                        </div>
                                    </div>
                                ) : (
                                    <div className="space-y-4 py-4">
                                        <div className="space-y-2">
                                            <label className="text-sm font-medium">Name</label>
                                            <Input placeholder="e.g. Slack Webhook" value={webhookName}
                                                   onChange={(e) => setWebhookName(e.target.value)}/>
                                        </div>
                                        <div className="space-y-2">
                                            <label className="text-sm font-medium">Payload URL</label>
                                            <Input placeholder="https://example.com/webhook" value={webhookUrl}
                                                   onChange={(e) => setWebhookUrl(e.target.value)}/>
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
                                            <TableCell colSpan={4}><Skeleton className="h-12 w-full"/></TableCell>
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
                            <Globe className="h-3.5 w-3.5 text-muted-foreground"/>
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
                                                        <Trash2 className="h-4 w-4"/>
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