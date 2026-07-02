import { useEffect } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';
import type { Webhook } from '@/api/types';
import { useUpdateWebhook } from '@/api/queries';
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
import { toast } from 'sonner';

const editWebhookSchema = z.object({
    name: z.string().min(1, 'Webhook name is required'),
    url: z.string().url('Must be a valid URL'),
    flagTagsStr: z.string().optional(),
    events: z.array(z.string()).min(1, 'Select at least one event')
});
type EditWebhookValues = z.infer<typeof editWebhookSchema>;

interface Props {
    webhook: Webhook | null;
    open: boolean;
    onOpenChange: (open: boolean) => void;
}

export function EditWebhookModal({ webhook, open, onOpenChange }: Props) {
    const updateWebhook = useUpdateWebhook(webhook?.projectId ?? '');

    const form = useForm<EditWebhookValues>({
        resolver: zodResolver(editWebhookSchema),
        defaultValues: { name: '', url: '', flagTagsStr: '', events: [] }
    });

    useEffect(() => {
        if (webhook && open) {
            form.reset({
                name: webhook.name,
                url: webhook.url,
                flagTagsStr: webhook.flagTags?.join(', ') || '',
                events: webhook.events || []
            });
        }
    }, [webhook, open, form]);

    const handleUpdateWebhookSubmit = async (values: EditWebhookValues) => {
        if (!webhook) return;
        try {
            await updateWebhook.mutateAsync({
                webhookId: webhook.id,
                data: {
                    name: values.name,
                    url: values.url,
                    events: values.events,
                    environmentIds: webhook.environmentIds,
                    flagTags: values.flagTagsStr ? values.flagTagsStr.split(',').map(t => t.trim()).filter(Boolean) : []
                }
            });
            toast.success('Webhook updated successfully');
            onOpenChange(false);
        } catch (error: any) {
            handleApiError(error, form.setError, 'Failed to update webhook');
        }
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent
                className="border-border/40 bg-zinc-950"
            >
                <DialogHeader>
                    <DialogTitle>Edit Webhook</DialogTitle>
                    <DialogDescription>Update webhook name, URL, or events.</DialogDescription>
                </DialogHeader>

                <Form {...form}>
                    <form onSubmit={form.handleSubmit(handleUpdateWebhookSubmit)}>
                        <div className="space-y-4 py-4">
                            <FormField
                                control={form.control}
                                name="name"
                                render={({ field }) => (
                                    <FormItem>
                                        <FormLabel>Name</FormLabel>
                                        <FormControl>
                                            <Input placeholder="e.g. Slack Webhook" {...field} />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />
                            <FormField
                                control={form.control}
                                name="url"
                                render={({ field }) => (
                                    <FormItem>
                                        <FormLabel>Payload URL</FormLabel>
                                        <FormControl>
                                            <Input placeholder="https://example.com/webhook" {...field} />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />
                            <FormField
                                control={form.control}
                                name="flagTagsStr"
                                render={({ field }) => (
                                    <FormItem>
                                        <FormLabel>Filter by Tags (optional)</FormLabel>
                                        <FormControl>
                                            <Input placeholder="e.g. backend, team-a (comma separated)" {...field} value={field.value || ''} />
                                        </FormControl>
                                        <p className="text-[10px] text-zinc-500 mt-1">Only send events for flags with matching tags.</p>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />
                            <FormField
                                control={form.control}
                                name="events"
                                render={() => (
                                    <FormItem>
                                        <FormLabel>Events</FormLabel>
                                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                                            {['flag.created', 'flag.updated', 'flag.deleted', 'experiment.winner_found', 'experiment.degraded'].map((evt) => (
                                                <FormField
                                                    key={evt}
                                                    control={form.control}
                                                    name="events"
                                                    render={({ field }) => {
                                                        return (
                                                            <FormItem
                                                                key={evt}
                                                                className="flex items-center space-x-2 space-y-0"
                                                            >
                                                                <FormControl>
                                                                    <Checkbox
                                                                        checked={field.value?.includes(evt)}
                                                                        onCheckedChange={(checked) => {
                                                                            return checked
                                                                                ? field.onChange([...field.value, evt])
                                                                                : field.onChange(
                                                                                    field.value?.filter(
                                                                                        (value) => value !== evt
                                                                                    )
                                                                                )
                                                                        }}
                                                                    />
                                                                </FormControl>
                                                                <FormLabel className="text-xs font-mono font-normal">
                                                                    {evt}
                                                                </FormLabel>
                                                            </FormItem>
                                                        )
                                                    }}
                                                />
                                            ))}
                                        </div>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />
                        </div>

                        <DialogFooter>
                            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
                            <Button type="submit" disabled={updateWebhook.isPending}>Save</Button>
                        </DialogFooter>
                    </form>
                </Form>
            </DialogContent>
        </Dialog>
    );
}
