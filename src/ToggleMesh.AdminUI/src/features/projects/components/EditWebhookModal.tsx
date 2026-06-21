import { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';
import type { Webhook } from '@/api/types';
import { useUpdateWebhook } from '@/api/queries';

interface Props {
    webhook: Webhook | null;
    open: boolean;
    onOpenChange: (open: boolean) => void;
}

export function EditWebhookModal({ webhook, open, onOpenChange }: Props) {
    const updateWebhook = useUpdateWebhook(webhook?.projectId ?? '');

    const [webhookName, setWebhookName] = useState('');
    const [webhookUrl, setWebhookUrl] = useState('');
    const [selectedEvents, setSelectedEvents] = useState<string[]>([]);

    useEffect(() => {
        if (webhook) {
            setWebhookName(webhook.name);
            setWebhookUrl(webhook.url);
            setSelectedEvents(webhook.events || []);
        }
    }, [webhook]);

    const handleUpdateWebhook = async () => {
        if (!webhook) return;
        await updateWebhook.mutateAsync({
            webhookId: webhook.id,
            data: {
                name: webhookName,
                url: webhookUrl,
                events: selectedEvents,
                environmentIds: webhook.environmentIds
            }
        });
        onOpenChange(false);
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent 
                className="border-border/40 bg-zinc-950"
                onKeyDown={(e) => {
                    if (e.key === 'Enter' && webhookName.trim() && webhookUrl.trim()) {
                        handleUpdateWebhook();
                    }
                }}
            >
                <DialogHeader>
                    <DialogTitle>Edit Webhook</DialogTitle>
                    <DialogDescription>Update webhook name, URL, or events.</DialogDescription>
                </DialogHeader>

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
                                        id={`edit-${evt}`}
                                        checked={selectedEvents.includes(evt)}
                                        onCheckedChange={(checked) => {
                                            if (checked) setSelectedEvents([...selectedEvents, evt]);
                                            else setSelectedEvents(selectedEvents.filter(x => x !== evt));
                                        }}
                                    />
                                    <label htmlFor={`edit-${evt}`} className="text-xs font-mono">{evt}</label>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>

                <DialogFooter>
                    <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
                    <Button onClick={handleUpdateWebhook} disabled={updateWebhook.isPending}>Save</Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
