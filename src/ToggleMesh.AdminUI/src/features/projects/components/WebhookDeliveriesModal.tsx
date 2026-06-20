import { useState } from 'react';
import { useWebhookDeliveries, useRetryWebhookDelivery, useCancelWebhookDelivery } from '@/api/queries';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Loader2, RefreshCw, ChevronLeft, ChevronRight, Activity, XCircle } from 'lucide-react';
import { WebhookDeliveryStatus } from '@/api/types';
import type { Webhook } from '@/api/types';

interface Props {
    webhook: Webhook | null;
    open: boolean;
    onOpenChange: (open: boolean) => void;
}

export function WebhookDeliveriesModal({ webhook, open, onOpenChange }: Props) {
    const [page, setPage] = useState(1);

    const { data, isLoading } = useWebhookDeliveries(
        webhook?.projectId ?? '',
        webhook?.id ?? '',
        page
    );

    const retryMutation = useRetryWebhookDelivery(webhook?.projectId ?? '', webhook?.id ?? '');
    const cancelMutation = useCancelWebhookDelivery(webhook?.projectId ?? '', webhook?.id ?? '');

    const handleRetry = async (deliveryId: string) => {
        await retryMutation.mutateAsync(deliveryId);
    };

    const handleCancel = async (deliveryId: string) => {
        await cancelMutation.mutateAsync(deliveryId);
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="max-w-4xl max-h-[85vh] overflow-y-auto">
                <DialogHeader>
                    <DialogTitle className="flex items-center gap-2">
                        <Activity className="h-5 w-5 text-primary" />
                        Delivery History: {webhook?.name}
                    </DialogTitle>
                    <DialogDescription>
                        View recent webhook events, payloads, and response codes.
                    </DialogDescription>
                </DialogHeader>

                <div className="mt-4 space-y-4">
                    {isLoading ? (
                        <div className="flex justify-center p-8">
                            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
                        </div>
                    ) : !data || data.items.length === 0 ? (
                        <div className="text-center p-8 border rounded-lg border-dashed bg-zinc-950/20 text-muted-foreground">
                            No deliveries recorded yet.
                        </div>
                    ) : (
                        <div className="space-y-4">
                            {data.items.map((delivery: any) => (
                                <div key={delivery.id} className="border border-border/40 rounded-lg p-4 bg-zinc-950/20 shadow-sm space-y-3">
                                    <div className="flex items-center justify-between flex-wrap gap-2">
                                        <div className="flex items-center gap-3">
                                            <Badge variant="outline" className={`font-mono text-[10px] uppercase tracking-wider
                                                ${delivery.status === WebhookDeliveryStatus.Success ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20' :
                                                    delivery.status === WebhookDeliveryStatus.Pending ? (delivery.attemptCount > 0 ? 'bg-orange-500/10 text-orange-400 border-orange-500/20' : 'bg-blue-500/10 text-blue-400 border-blue-500/20') :
                                                    delivery.status === WebhookDeliveryStatus.Canceled ? 'bg-zinc-500/10 text-zinc-400 border-zinc-500/20' :
                                                        'bg-rose-500/10 text-rose-400 border-rose-500/20'}`}>
                                                {delivery.status === WebhookDeliveryStatus.Success ? 'Success' :
                                                    delivery.status === WebhookDeliveryStatus.Pending ? (delivery.attemptCount > 0 ? 'Failing (Retrying)' : 'Pending') :
                                                    delivery.status === WebhookDeliveryStatus.Canceled ? 'Canceled' : 'Failed'}
                                            </Badge>

                                            <span className="font-semibold text-sm">{delivery.eventName}</span>

                                            {delivery.statusCode && (
                                                <Badge variant="secondary" className="text-xs font-mono">
                                                    HTTP {delivery.statusCode}
                                                </Badge>
                                            )}
                                        </div>

                                        <div className="flex items-center gap-3">
                                            <span className="text-xs text-muted-foreground">
                                                {new Intl.DateTimeFormat('en-US', {
                                                    month: 'short', day: 'numeric', year: 'numeric',
                                                    hour: '2-digit', minute: '2-digit', second: '2-digit'
                                                }).format(new Date(delivery.createdAt))}
                                            </span>

                                            {delivery.status === WebhookDeliveryStatus.Pending && (
                                                <Button
                                                    size="sm"
                                                    variant="ghost"
                                                    className="h-7 text-xs text-rose-400 hover:text-rose-300 hover:bg-rose-500/10"
                                                    onClick={() => handleCancel(delivery.id)}
                                                    disabled={cancelMutation.isPending}
                                                >
                                                    <XCircle className={`h-3 w-3 mr-1 ${cancelMutation.isPending ? 'animate-spin' : ''}`} />
                                                    Cancel
                                                </Button>
                                            )}

                                            <Button
                                                size="sm"
                                                variant="outline"
                                                className="h-7 text-xs"
                                                onClick={() => handleRetry(delivery.id)}
                                                disabled={retryMutation.isPending || delivery.status === WebhookDeliveryStatus.Success}
                                            >
                                                <RefreshCw className={`h-3 w-3 mr-1 ${retryMutation.isPending ? 'animate-spin' : ''}`} />
                                                Retry
                                            </Button>
                                        </div>
                                    </div>

                                    {delivery.errorMessage && (
                                        <div className="text-xs text-rose-400 bg-rose-500/10 p-2 rounded border border-rose-500/20 font-mono">
                                            Error: {delivery.errorMessage}
                                        </div>
                                    )}

                                    <details className="text-xs group cursor-pointer">
                                        <summary className="text-muted-foreground hover:text-foreground transition-colors outline-none font-medium mb-2">
                                            View Payload
                                        </summary>
                                        <pre className="bg-zinc-950 p-3 rounded-md overflow-x-auto text-[11px] text-zinc-300 font-mono border border-border/30 mt-2">
                                            {JSON.stringify(JSON.parse(delivery.payload), null, 2)}
                                        </pre>
                                    </details>

                                    {delivery.attemptCount > 1 && (
                                        <div className="text-[10px] text-muted-foreground flex items-center justify-between border-t border-border/20 pt-2 mt-2">
                                            <span>Attempts: {delivery.attemptCount}/5</span>
                                            {delivery.nextAttemptAt && delivery.status === WebhookDeliveryStatus.Pending && (
                                                <span>Next retry: {new Intl.DateTimeFormat('en-US', {
                                                    hour: '2-digit', minute: '2-digit', second: '2-digit'
                                                }).format(new Date(delivery.nextAttemptAt))}</span>
                                            )}
                                        </div>
                                    )}
                                </div>
                            ))}

                            <div className="flex items-center justify-between pt-2">
                                <span className="text-xs text-muted-foreground">
                                    Page {page} of {Math.ceil(data.totalCount / data.pageSize)} ({data.totalCount} total)
                                </span>
                                <div className="flex gap-2">
                                    <Button
                                        variant="outline"
                                        size="sm"
                                        onClick={() => setPage(p => Math.max(1, p - 1))}
                                        disabled={page === 1}
                                        className="h-8 w-8 p-0"
                                    >
                                        <ChevronLeft className="h-4 w-4" />
                                    </Button>
                                    <Button
                                        variant="outline"
                                        size="sm"
                                        onClick={() => setPage(p => p + 1)}
                                        disabled={!data.hasNextPage}
                                        className="h-8 w-8 p-0"
                                    >
                                        <ChevronRight className="h-4 w-4" />
                                    </Button>
                                </div>
                            </div>
                        </div>
                    )}
                </div>
            </DialogContent>
        </Dialog>
    );
}
