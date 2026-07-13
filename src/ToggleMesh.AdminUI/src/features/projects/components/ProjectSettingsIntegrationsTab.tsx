import { useState } from 'react';
import {
    useProjectIntegrations,
    useCreateIntegration,
    useDeleteIntegration,
    useTestIntegration
} from '@/api/queries';
import { IntegrationProvider, type Integration } from '@/api/types';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Trash2, Plus, Settings, Play, Activity } from 'lucide-react';
import { toast } from 'sonner';
import { EmptyState } from "@/components/EmptyState.tsx";
import { EditIntegrationModal } from './EditIntegrationModal';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import { Form, FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { handleApiError } from '@/api/errorUtils';
import { EventSelectionCheckboxes } from './EventSelectionCheckboxes';

const createIntegrationSchema = z.object({
    name: z.string().min(1, 'Name is required'),
    webhookUrl: z.string().url('Invalid URL'),
    provider: z.enum([IntegrationProvider.Slack, IntegrationProvider.Discord, IntegrationProvider.MicrosoftTeams]),
    events: z.array(z.string()).min(1, 'At least one event must be selected'),
    environmentIds: z.array(z.string())
});

type CreateIntegrationValues = z.infer<typeof createIntegrationSchema>;

export function ProjectSettingsIntegrationsTab({ projectId }: { projectId: string }) {
    const { data: integrations, isLoading } = useProjectIntegrations(projectId);
    const createIntegration = useCreateIntegration(projectId);
    const deleteIntegration = useDeleteIntegration(projectId);
    const testIntegration = useTestIntegration(projectId);

    const [isCreateOpen, setIsCreateOpen] = useState(false);
    const [integrationToDelete, setIntegrationToDelete] = useState<string | null>(null);
    const [editingIntegration, setEditingIntegration] = useState<Integration | null>(null);

    const form = useForm<CreateIntegrationValues>({
        resolver: zodResolver(createIntegrationSchema),
        defaultValues: { name: '', webhookUrl: '', provider: IntegrationProvider.Slack, events: [], environmentIds: [] },
    });

    const onSubmit = async (values: CreateIntegrationValues) => {
        try {
            await createIntegration.mutateAsync(values);
            toast.success('Integration created successfully');
            setIsCreateOpen(false);
            form.reset();
        } catch (error: any) {
            handleApiError(error, form.setError, 'Failed to create integration');
        }
    };

    const handleDelete = async () => {
        if (!integrationToDelete) return;
        try {
            await deleteIntegration.mutateAsync(integrationToDelete);
            toast.success('Integration deleted');
            setIntegrationToDelete(null);
        } catch (error) {
            toast.error('Failed to delete integration');
        }
    };

    const handleTest = async (id: string) => {
        try {
            await testIntegration.mutateAsync(id);
            toast.success('Test message sent successfully');
        } catch (error) {
            toast.error('Failed to test integration');
        }
    };



    return (
        <div className="space-y-6">
            <div className="flex justify-between items-center">
                <div>
                    <h3 className="text-lg font-medium">Outbound Integrations</h3>
                    <p className="text-sm text-muted-foreground">
                        Send notifications to Slack, Discord, or Teams when important events happen.
                    </p>
                </div>
                <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
                    <DialogTrigger asChild>
                        <Button>
                            <Plus className="mr-2 h-4 w-4" />
                            Add Integration
                        </Button>
                    </DialogTrigger>
                    <DialogContent className="sm:max-w-[500px]">
                        <DialogHeader>
                            <DialogTitle>Add Integration</DialogTitle>
                            <DialogDescription>
                                Configure a new outbound integration.
                            </DialogDescription>
                        </DialogHeader>

                        <Form {...form}>
                            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                                <FormField
                                    control={form.control}
                                    name="provider"
                                    render={({ field }) => (
                                        <FormItem>
                                            <div className="text-sm font-medium">Provider</div>
                                            <div className="grid grid-cols-3 gap-3">
                                                {Object.entries(IntegrationProvider).map(([key, value]) => (
                                                    <div
                                                        key={key}
                                                        onClick={() => field.onChange(value)}
                                                        className={`flex items-center justify-center px-4 py-3 border rounded-md cursor-pointer transition-all ${field.value === value
                                                                ? 'border-primary bg-primary/10 text-primary ring-1 ring-primary'
                                                                : 'border-border/40 bg-zinc-950/40 hover:border-zinc-700 hover:bg-zinc-900/50 text-muted-foreground'
                                                            }`}
                                                    >
                                                        <span className={`text-sm font-medium ${field.value === value ? 'text-primary' : 'text-zinc-300'}`}>{key}</span>
                                                    </div>
                                                ))}
                                            </div>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />

                                <FormField
                                    control={form.control}
                                    name="name"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormControl>
                                                <Input placeholder="Integration Name (e.g., Engineering Team Slack)" {...field} />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                                <FormField
                                    control={form.control}
                                    name="webhookUrl"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormControl>
                                                <Input placeholder="Webhook URL" {...field} />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />

                                <EventSelectionCheckboxes form={form} name="events" />
                                <DialogFooter className="mt-6">
                                    <Button type="button" variant="outline" onClick={() => setIsCreateOpen(false)}>
                                        Cancel
                                    </Button>
                                    <Button type="submit" disabled={createIntegration.isPending}>
                                        {createIntegration.isPending ? 'Saving...' : 'Add Integration'}
                                    </Button>
                                </DialogFooter>
                            </form>
                        </Form>
                    </DialogContent>
                </Dialog>
            </div>

            <Card className="border-border/40 bg-zinc-950/20">
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>Name</TableHead>
                            <TableHead>Provider</TableHead>
                            <TableHead>Events</TableHead>
                            <TableHead className="w-[100px]">Status</TableHead>
                            <TableHead className="text-right">Actions</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {isLoading ? (
                            <TableRow>
                                <TableCell colSpan={5}><Skeleton className="h-12 w-full" /></TableCell>
                            </TableRow>
                        ) : integrations?.length === 0 ? (
                            <TableRow>
                                <TableCell colSpan={5} className="p-0">
                                    <EmptyState
                                        icon={Activity}
                                        title="No Integrations Configured"
                                        description="Set up Slack, Discord, or Teams to receive notifications."
                                        action={
                                            <Button onClick={() => setIsCreateOpen(true)} variant="outline">
                                                <Plus className="mr-2 h-4 w-4" />
                                                Add Integration
                                            </Button>
                                        }
                                    />
                                </TableCell>
                            </TableRow>
                        ) : integrations?.map((integration) => (
                            <TableRow key={integration.id}>
                                <TableCell className="font-medium">{integration.name}</TableCell>
                                <TableCell>
                                    <Badge variant="outline">{integration.provider}</Badge>
                                </TableCell>
                                <TableCell>
                                    <div className="flex flex-wrap gap-1">
                                        {integration.events.map(e => (
                                            <Badge key={e} variant="secondary" className="text-xs">
                                                {e}
                                            </Badge>
                                        ))}
                                    </div>
                                </TableCell>
                                <TableCell>
                                    <Badge variant={integration.isActive ? "default" : "destructive"}>
                                        {integration.isActive ? 'Active' : 'Inactive'}
                                    </Badge>
                                </TableCell>
                                <TableCell className="text-right">
                                    <div className="flex justify-end gap-2">
                                        <Button
                                            variant="ghost"
                                            size="icon"
                                            onClick={() => handleTest(integration.id)}
                                            title="Send Test Message"
                                        >
                                            <Play className="h-4 w-4" />
                                        </Button>
                                        <Button
                                            variant="ghost"
                                            size="icon"
                                            onClick={() => setEditingIntegration(integration)}
                                        >
                                            <Settings className="h-4 w-4" />
                                        </Button>
                                        <Button
                                            variant="ghost"
                                            size="icon"
                                            className="text-destructive hover:text-destructive"
                                            onClick={() => setIntegrationToDelete(integration.id)}
                                        >
                                            <Trash2 className="h-4 w-4" />
                                        </Button>
                                    </div>
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </Card>

            {editingIntegration && (
                <EditIntegrationModal
                    isOpen={!!editingIntegration}
                    onClose={() => setEditingIntegration(null)}
                    integration={editingIntegration}
                    projectId={projectId}
                />
            )}

            <Dialog open={!!integrationToDelete} onOpenChange={(open) => !open && setIntegrationToDelete(null)}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Delete Integration</DialogTitle>
                        <DialogDescription>
                            Are you sure you want to delete this integration? This action cannot be undone.
                        </DialogDescription>
                    </DialogHeader>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setIntegrationToDelete(null)}>Cancel</Button>
                        <Button
                            variant="destructive"
                            onClick={handleDelete}
                            disabled={deleteIntegration.isPending}
                        >
                            {deleteIntegration.isPending ? 'Deleting...' : 'Delete'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
