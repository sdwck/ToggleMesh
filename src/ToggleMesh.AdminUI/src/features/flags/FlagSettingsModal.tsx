import { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tag, Shield, ShieldOff, ShieldCheck } from 'lucide-react';
import { useUpdateGlobalFlagSettings, useProjectDetails, useUpdateFlagProtection } from '@/api/queries';
import { toast } from 'sonner';
import { type ProjectFlagDto, ProjectRole } from '@/api/types';
import { VariationsManager } from './components/VariationsManager';
import { toastApiError } from '@/api/errorUtils';

interface FlagSettingsModalProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    projectId: string;
    flag: ProjectFlagDto;
}

export function FlagSettingsModal({ open, onOpenChange, projectId, flag }: FlagSettingsModalProps) {
    const [name, setName] = useState(flag.name || '');
    const [description, setDescription] = useState(flag.description || '');
    const [tags, setTags] = useState(flag.tags?.join(', ') || '');
    const [variations, setVariations] = useState(flag.variations?.map(v => ({ id: v.id, value: v.value })) || []);
    const [isConfirmUnprotectOpen, setIsConfirmUnprotectOpen] = useState(false);

    const { data: project } = useProjectDetails(projectId);
    const updateProtection = useUpdateFlagProtection(projectId, flag.key);

    const isOwnerOrAdmin = project?.userRole === ProjectRole.Owner || project?.userRole === ProjectRole.Admin;
    const anyEnvRequiresApprovals = project?.environments?.some(e => e.requireApprovals) ?? false;

    useEffect(() => {
        if (open) {
            setName(flag.name || '');
            setDescription(flag.description || '');
            setTags(flag.tags?.join(', ') || '');
            setVariations(flag.variations?.map(v => ({ id: v.id, value: v.value })) || []);
        }
    }, [open, flag]);

    const updateSettings = useUpdateGlobalFlagSettings(projectId, flag.key);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        if (flag.type !== 0 && variations.length === 0) {
            toast.error('At least one variation is required');
            return;
        }

        try {
            await updateSettings.mutateAsync({
                name: name.trim() || null,
                description: description.trim() || null,
                tags: tags.split(',').map(t => t.trim()).filter(Boolean),
                variations: flag.type === 0 ? [] : variations
            });
            toast.success('Flag settings updated');
            onOpenChange(false);
        } catch (err) {
            toastApiError(err, 'Failed to update flag settings');
        }
    };

    return (
        <>
            <Dialog open={open} onOpenChange={onOpenChange}>
                <DialogContent className="sm:max-w-[600px] bg-zinc-950 border-zinc-800">
                    <DialogHeader>
                        <DialogTitle className="flex items-center gap-2">
                            Flag Settings
                        </DialogTitle>
                        <DialogDescription>
                            Manage global settings for <span className="font-mono text-zinc-200">{flag.key}</span>. These changes apply across all environments.
                        </DialogDescription>
                    </DialogHeader>

                    <form onSubmit={handleSubmit} className="space-y-6 mt-4">
                        <div className="space-y-4">
                            <div className="space-y-2">
                                <Label htmlFor="name">Name</Label>
                                <Input
                                    id="name"
                                    value={name}
                                    onChange={(e) => setName(e.target.value)}
                                    placeholder="Human-readable name"
                                    className="bg-zinc-900/50"
                                />
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="description">Description</Label>
                                <Textarea
                                    id="description"
                                    value={description}
                                    onChange={(e) => setDescription(e.target.value)}
                                    placeholder="Brief description of this feature flag"
                                    rows={3}
                                    className="resize-none bg-zinc-900/50"
                                />
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="tags" className="flex items-center gap-1">
                                    <Tag className="h-3.5 w-3.5" /> Tags <span className="text-[10px] text-muted-foreground font-normal">(Global, comma-separated)</span>
                                </Label>
                                <Input
                                    id="tags"
                                    value={tags}
                                    onChange={(e) => setTags(e.target.value)}
                                    placeholder="e.g. billing, staging-only, stale"
                                    className="bg-zinc-900/50"
                                />
                            </div>

                            {isOwnerOrAdmin && anyEnvRequiresApprovals && (
                                <div className="pt-4 border-t border-border/40 space-y-3">
                                    <div className="flex items-center justify-between gap-4">
                                        <div className="space-y-0.5">
                                            <Label className="text-sm font-medium flex items-center gap-1.5">
                                                <ShieldCheck className="h-4 w-4 text-emerald-400" /> Flag Protection
                                            </Label>
                                            <p className="text-xs text-muted-foreground">
                                                Protected flags require review & approvals before applying changes to protected environments.
                                            </p>
                                        </div>
                                        {flag.isProtected ? (
                                            <Button
                                                type="button"
                                                variant="outline"
                                                size="sm"
                                                disabled={updateProtection.isPending}
                                                onClick={() => setIsConfirmUnprotectOpen(true)}
                                                className="text-rose-400 border-rose-500/30 hover:bg-rose-500/10 text-xs shrink-0 cursor-pointer"
                                            >
                                                <ShieldOff className="mr-1.5 h-3.5 w-3.5" /> Unprotect Flag
                                            </Button>
                                        ) : (
                                            <Button
                                                type="button"
                                                variant="outline"
                                                size="sm"
                                                disabled={updateProtection.isPending}
                                                onClick={async () => {
                                                    try {
                                                        await updateProtection.mutateAsync(true);
                                                        toast.success('Flag is now protected');
                                                    } catch (err: any) {
                                                        toast.error(err?.response?.data?.message || 'Failed to protect flag');
                                                    }
                                                }}
                                                className="border-border/40 hover:bg-zinc-800/60 text-xs shrink-0 cursor-pointer"
                                            >
                                                <Shield className="mr-1.5 h-3.5 w-3.5 text-blue-400" /> Protect Flag
                                            </Button>
                                        )}
                                    </div>
                                </div>
                            )}

                            {flag.type !== 0 && (
                                <div className="pt-4 border-t border-border/40">
                                    <h4 className="text-sm font-medium mb-4">Variations</h4>
                                    <VariationsManager
                                        type={flag.type === 2 ? 'JSON' : 'String'}
                                        variations={variations}
                                        onChange={setVariations}
                                        originalVariationIds={flag.variations?.map(v => v.id)}
                                    />
                                </div>
                            )}
                        </div>

                        <DialogFooter className="border-t border-border/40 pt-4 mt-6">
                            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                                Cancel
                            </Button>
                            <Button type="submit" disabled={updateSettings.isPending}>
                                {updateSettings.isPending ? 'Saving...' : 'Save Settings'}
                            </Button>
                        </DialogFooter>
                    </form>
                </DialogContent>
            </Dialog>

            <Dialog open={isConfirmUnprotectOpen} onOpenChange={setIsConfirmUnprotectOpen}>
                <DialogContent className="bg-zinc-950 border-zinc-800/60">
                    <DialogHeader>
                        <DialogTitle>Unprotect Flag</DialogTitle>
                        <DialogDescription>
                            Are you sure you want to remove protection from this flag? This will disable the requirement for change requests in affected environments.
                        </DialogDescription>
                    </DialogHeader>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setIsConfirmUnprotectOpen(false)}>Cancel</Button>
                        <Button variant="destructive" disabled={updateProtection.isPending} onClick={async () => {
                            try {
                                await updateProtection.mutateAsync(false);
                                toast.success('Protection removed');
                                setIsConfirmUnprotectOpen(false);
                            } catch (err: any) {
                                toast.error(err?.response?.data?.message || 'Failed to remove protection');
                            }
                        }}>
                            Confirm Unprotect
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </>
    );
}
