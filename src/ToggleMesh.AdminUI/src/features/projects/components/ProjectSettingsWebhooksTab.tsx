import { useState, useEffect } from 'react';
import {
    useProjectWebhooks,
    useCreateWebhook,
    useDeleteWebhook,
    useUpdateWebhookStatus
} from '@/api/queries';
import { WebhookStatus, type Webhook } from '@/api/types';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Trash2, Plus, Copy, Globe, Activity, Settings, Pause, Play } from 'lucide-react';
import { toast } from 'sonner';
import { EmptyState } from "@/components/EmptyState.tsx";
import { WebhookDeliveriesModal } from './WebhookDeliveriesModal';
import { EditWebhookModal } from './EditWebhookModal';
import { EventSelectionCheckboxes } from './EventSelectionCheckboxes';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import { Form, FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { handleApiError } from '@/api/errorUtils';

const createWebhookSchema = z.object({
    name: z.string().min(1, 'Webhook name is required'),
    url: z.string().url('Invalid URL'),
    events: z.array(z.string()).min(1, 'At least one event must be selected'),
    flagTagsStr: z.string().optional(),
});
type CreateWebhookValues = z.infer<typeof createWebhookSchema>;

export function ProjectSettingsWebhooksTab({ projectId }: { projectId: string }) {

    const { data: webhooks, isLoading: isWebhooksLoading } = useProjectWebhooks(projectId);
    const createWebhook = useCreateWebhook(projectId);
    const deleteWebhook = useDeleteWebhook(projectId);
    const updateWebhookStatus = useUpdateWebhookStatus(projectId);

    const [isWebhookOpen, setIsWebhookOpen] = useState(false);
    const [revealedSecret, setRevealedSecret] = useState<string | null>(null);
    const [webhookToDelete, setWebhookToDelete] = useState<string | null>(null);
    const [deliveriesWebhook, setDeliveriesWebhook] = useState<Webhook | null>(null);
    const [editingWebhook, setEditingWebhook] = useState<Webhook | null>(null);

    const webhookForm = useForm<CreateWebhookValues>({
        resolver: zodResolver(createWebhookSchema),
        defaultValues: { name: '', url: '', events: [], flagTagsStr: '' },
    });

    useEffect(() => {
        if (isWebhookOpen) {
            webhookForm.reset({ name: '', url: '', events: [], flagTagsStr: '' });
        }
    }, [isWebhookOpen, webhookForm]);

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

    const handleToggleWebhookStatus = async (hook: Webhook) => {
        try {
            const newStatus = hook.status === WebhookStatus.Active ? WebhookStatus.Paused : WebhookStatus.Active;
            await updateWebhookStatus.mutateAsync({ webhookId: hook.id, status: newStatus });
            toast.success(`Webhook ${newStatus === WebhookStatus.Active ? 'resumed' : 'paused'}`);
        } catch {
            toast.error('Failed to update webhook status');
        }
    };

    const handleCreateWebhookSubmit = async (values: CreateWebhookValues) => {
        try {
            const response = await createWebhook.mutateAsync({
                name: values.name.trim(),
                url: values.url.trim(),
                environmentIds: [],
                events: values.events,
                flagTags: values.flagTagsStr ? values.flagTagsStr.split(',').map(t => t.trim()).filter(Boolean) : []
            });
            setRevealedSecret(response.secretKey);
            toast.success('Webhook created successfully');
        } catch (error: any) {
            handleApiError(error, webhookForm.setError, 'Failed to create webhook');
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
            <div className="flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4">
                <div>
                    <h3 className="text-lg font-medium">Outgoing Webhooks</h3>
                    <p className="text-sm text-muted-foreground">Deliver real-time payloads upon environment and flag changes.</p>
                </div>
                <Dialog open={isWebhookOpen} onOpenChange={(open) => {
                    setIsWebhookOpen(open);
                    if (!open) setRevealedSecret(null);
                }}>
                    <DialogTrigger asChild>
                        <Button size="sm" className="self-start sm:self-auto">
                            <Plus className="mr-2 h-4 w-4" /> Add Webhook
                        </Button>
                    </DialogTrigger>
                    <DialogContent className="border-border/40 bg-zinc-950">
                        <DialogHeader>
                            <DialogTitle>Configure Webhook</DialogTitle>
                            <DialogDescription>Add a new integration URL to push notifications.</DialogDescription>
                        </DialogHeader>

                        {revealedSecret ? (
                            <div className="space-y-4 py-4">
                                <div className="text-sm text-destructive font-semibold">
                                    Copy this secret now! You will not be able to view it again.
                                </div>
                                <div className="flex items-center gap-2">
                                    <Input value={revealedSecret} readOnly className="font-mono text-xs bg-muted/40" />
                                    <Button variant="outline" size="icon" onClick={handleCopySecret}>
                                        <Copy className="h-4 w-4" />
                                    </Button>
                                </div>
                            </div>
                        ) : (
                            <Form {...webhookForm}>
                                <form onSubmit={webhookForm.handleSubmit(handleCreateWebhookSubmit)}>
                                    <div className="space-y-4 py-4">
                                        <div className="space-y-2">
                                            <label className="text-sm font-medium">Name</label>
                                            <FormField
                                                control={webhookForm.control}
                                                name="name"
                                                render={({ field }) => (
                                                    <FormItem>
                                                        <FormControl>
                                                            <Input {...field} placeholder="e.g. Slack Webhook" />
                                                        </FormControl>
                                                        <FormMessage />
                                                    </FormItem>
                                                )}
                                            />
                                        </div>
                                        <div className="space-y-2">
                                            <label className="text-sm font-medium">Payload URL</label>
                                            <FormField
                                                control={webhookForm.control}
                                                name="url"
                                                render={({ field }) => (
                                                    <FormItem>
                                                        <FormControl>
                                                            <Input {...field} placeholder="https://example.com/webhook" />
                                                        </FormControl>
                                                        <FormMessage />
                                                    </FormItem>
                                                )}
                                            />
                                        </div>
                                        <div className="space-y-2">
                                            <label className="text-sm font-medium">Filter by Tags (optional)</label>
                                            <FormField
                                                control={webhookForm.control}
                                                name="flagTagsStr"
                                                render={({ field }) => (
                                                    <FormItem>
                                                        <FormControl>
                                                            <Input {...field} placeholder="e.g. backend, team-a (comma separated)" />
                                                        </FormControl>
                                                        <p className="text-[10px] text-zinc-500 mt-1">Only send events for flags with matching tags.</p>
                                                        <FormMessage />
                                                    </FormItem>
                                                )}
                                            />
                                        </div>
                                        <EventSelectionCheckboxes form={webhookForm} name="events" title="Events" />
                                    </div>
                                    <DialogFooter className="mt-4">
                                        <Button type="button" variant="outline" onClick={() => setIsWebhookOpen(false)}>Cancel</Button>
                                        <Button type="submit" disabled={createWebhook.isPending}>Save</Button>
                                    </DialogFooter>
                                </form>
                            </Form>
                        )}
                        {revealedSecret && (
                            <DialogFooter className="mt-4">
                                <Button onClick={() => setIsWebhookOpen(false)}>Done</Button>
                            </DialogFooter>
                        )}
                    </DialogContent>
                </Dialog>
            </div>

            <Card className="border-border/40 bg-zinc-950/20">
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>Name / URL</TableHead>
                            <TableHead>Events</TableHead>
                            <TableHead className="hidden md:table-cell">Triggered</TableHead>
                            <TableHead className="hidden md:table-cell">Status</TableHead>
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
                                            <span className="text-xs text-muted-foreground font-mono">{hook.url}</span>
                                        </div>
                                    </TableCell>
                                    <TableCell>
                                        <div className="flex gap-1.5 flex-wrap">
                                            {hook.events.length === 0 ? (
                                                <Badge variant="outline" className="text-[10px] font-mono text-zinc-500 border-zinc-700">NONE</Badge>
                                            ) : (
                                                hook.events.map((e: string) => <Badge key={e} variant="outline" className="text-[10px] font-mono">{e}</Badge>)
                                            )}
                                        </div>
                                        {hook.flagTags && hook.flagTags.length > 0 && (
                                            <div className="mt-2 text-[10px] text-zinc-400">
                                                Tags: <span className="font-mono text-zinc-300">{hook.flagTags.join(', ')}</span>
                                            </div>
                                        )}
                                    </TableCell>
                                    <TableCell className="hidden md:table-cell text-xs text-muted-foreground font-mono">
                                        {hook.lastTriggeredAt ? new Date(hook.lastTriggeredAt).toLocaleString() : 'Never'}
                                    </TableCell>
                                    <TableCell className="hidden md:table-cell">
                                        <div className="flex gap-2">
                                            {hook.status === WebhookStatus.Active && (
                                                <Badge variant="outline" className="bg-emerald-500/10 text-emerald-400 border-emerald-500/20 text-[10px]">Active</Badge>
                                            )}
                                            {hook.status === WebhookStatus.Paused && (
                                                <Badge variant="outline" className="bg-zinc-500/10 text-zinc-400 border-zinc-500/20 text-[10px]">Paused</Badge>
                                            )}
                                            {hook.status === WebhookStatus.Failing && (
                                                <Badge variant="outline" className="bg-amber-500/10 text-amber-400 border-amber-500/20 text-[10px]">Failing ({hook.consecutiveFailures}/10)</Badge>
                                            )}
                                            {hook.status === WebhookStatus.DisabledBySystem && (
                                                <Badge variant="outline" className="bg-rose-500/10 text-rose-400 border-rose-500/20 text-[10px]">Disabled</Badge>
                                            )}
                                        </div>
                                    </TableCell>
                                    <TableCell className="text-right">
                                        <div className="flex justify-end gap-1">
                                            <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-blue-400" onClick={() => setDeliveriesWebhook(hook)}>
                                                <Activity className="h-4 w-4" />
                                            </Button>
                                            <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-amber-400" title={hook.status === WebhookStatus.Paused ? 'Resume Webhook' : 'Pause Webhook'} onClick={() => handleToggleWebhookStatus(hook)}>
                                                {hook.status === WebhookStatus.Paused ? <Play className="h-4 w-4" /> : <Pause className="h-4 w-4" />}
                                            </Button>
                                            <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-primary" onClick={() => setEditingWebhook(hook)}>
                                                <Settings className="h-4 w-4" />
                                            </Button>
                                            <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-destructive" onClick={() => setWebhookToDelete(hook.id)}>
                                                <Trash2 className="h-4 w-4" />
                                            </Button>
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ))
                        )}
                    </TableBody>
                </Table>
            </Card>

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
                            {deleteWebhook.isPending ? 'Deleting...' : 'Delete Webhook'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            <WebhookDeliveriesModal webhook={deliveriesWebhook} open={!!deliveriesWebhook} onOpenChange={(open) => !open && setDeliveriesWebhook(null)} />
            <EditWebhookModal webhook={editingWebhook} open={!!editingWebhook} onOpenChange={(open) => !open && setEditingWebhook(null)} />
        </div>
    );
}
