import { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useRestoreExperimentSnapshot, useFeatureFlag } from '@/api/queries';
import { toast } from 'sonner';
import { AlertTriangle } from 'lucide-react';
import type { ExperimentIterationDto } from '@/api/types';

interface RestoreSnapshotModalProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    projectId: string;
    envId: string;
    flagKey: string;
    iteration: ExperimentIterationDto;
}

export function RestoreSnapshotModal({
    open,
    onOpenChange,
    projectId,
    envId,
    flagKey,
    iteration
}: RestoreSnapshotModalProps) {
    const restoreMutation = useRestoreExperimentSnapshot(projectId, envId, flagKey);
    const { data: currentFlagState } = useFeatureFlag(projectId, envId, flagKey);
    const [confirmText, setConfirmText] = useState('');

    const handleRestore = () => {
        restoreMutation.mutate(iteration.id, {
            onSuccess: () => {
                toast.success('Flag configuration has been restored from the snapshot.');
                setConfirmText('');
                onOpenChange(false);
            },
            onError: (err: any) => {
                toast.error(err.response?.data?.message || err.message || 'Failed to restore snapshot');
            }
        });
    };

    let snapshotJson = '{}';
    try {
        if (iteration.flagConfigSnapshot) {
            snapshotJson = JSON.stringify(JSON.parse(iteration.flagConfigSnapshot), null, 2);
        }
    } catch (e) {
        // ignore
    }

    let currentJson = '{}';
    try {
        if (currentFlagState) {
            const currentObj = {
                IsEnabled: currentFlagState.isEnabled,
                RolloutPercentage: currentFlagState.rolloutPercentage,
                IsMabEnabled: currentFlagState.isMabEnabled,
                ContextualRollouts: currentFlagState.contextualRollouts,
                ContextPartitionKeys: currentFlagState.contextPartitionKeys,
                Rules: currentFlagState.rules?.map((r: any) => ({
                    GroupId: r.groupId,
                    Attribute: r.attribute,
                    Operator: r.operator,
                    Value: r.value
                }))
            };
            currentJson = JSON.stringify(currentObj, null, 2);
        }
    } catch (e) {
        // ignore
    }

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="max-w-4xl max-h-[85vh] overflow-y-auto border-border/40 bg-zinc-950 z-[110]">
                <DialogHeader>
                    <DialogTitle>Restore Historical Snapshot</DialogTitle>
                    <DialogDescription>
                        You are about to overwrite the current flag configuration with the state from when this experiment ended on {new Date(iteration.endedAt).toLocaleString()}.
                    </DialogDescription>
                </DialogHeader>

                <div className="grid grid-cols-2 gap-4 mt-4">
                    <div className="space-y-2">
                        <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Current State</h4>
                        <div className="bg-rose-950/10 border border-rose-500/10 p-4 rounded-md font-mono text-xs overflow-x-auto whitespace-pre-wrap text-rose-400">
                            <pre>{currentJson}</pre>
                        </div>
                    </div>
                    <div className="space-y-2">
                        <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Snapshot State (Will Apply)</h4>
                        <div className="bg-emerald-950/10 border border-emerald-500/10 p-4 rounded-md font-mono text-xs overflow-x-auto whitespace-pre-wrap text-emerald-400">
                            <pre>{snapshotJson}</pre>
                        </div>
                    </div>
                </div>

                <div className="mt-6 p-4 rounded-lg bg-zinc-900/50 border border-zinc-800 flex flex-col gap-3">
                    <div className="flex gap-3">
                        <div className="mt-0.5 text-amber-500">
                            <AlertTriangle className="h-5 w-5" />
                        </div>
                        <div>
                            <h4 className="text-sm font-medium text-zinc-200">Confirmation Required</h4>
                            <div className="text-xs text-zinc-400 mt-1 leading-relaxed space-y-2">
                                <p>This action will immediately replace the current evaluation rules and rollout settings for the feature flag <strong>{flagKey}</strong>.</p>
                                <p>Type <span className="font-mono text-zinc-200 bg-zinc-800 px-1 rounded select-all">Confirm</span> below to authorize this rollback.</p>
                            </div>
                        </div>
                    </div>
                    <div className="ml-8 mt-1">
                        <Input 
                            value={confirmText}
                            onChange={(e) => setConfirmText(e.target.value)}
                            placeholder="Confirm"
                            className="max-w-xs bg-zinc-950/50 border-zinc-800 focus-visible:ring-emerald-500/50"
                        />
                    </div>
                </div>

                <DialogFooter className="mt-6 flex gap-3">
                    <Button variant="ghost" onClick={() => { onOpenChange(false); setConfirmText(''); }} disabled={restoreMutation.isPending} className="text-zinc-400 hover:text-zinc-100">
                        Cancel
                    </Button>
                    <Button 
                        onClick={handleRestore} 
                        disabled={restoreMutation.isPending || confirmText !== 'Confirm'}
                        className="bg-emerald-800 hover:bg-emerald-700 text-emerald-50 border border-emerald-700/50"
                    >
                        {restoreMutation.isPending ? 'Restoring...' : 'Confirm & Restore'}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
