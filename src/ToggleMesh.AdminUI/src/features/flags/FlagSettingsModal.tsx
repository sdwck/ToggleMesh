import { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tag } from 'lucide-react';
import { useUpdateGlobalFlagSettings } from '@/api/queries';
import { toast } from 'sonner';
import { type ProjectFlagDto } from '@/api/types';
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
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="sm:max-w-[600px] bg-zinc-950">
                <DialogHeader>
                    <DialogTitle>Flag Settings</DialogTitle>
                    <DialogDescription>
                        Manage global settings for {flag.key}. These changes apply across all environments.
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

                        {flag.type !== 0 && (
                            <div className="pt-4 border-t border-border/40">
                                <h4 className="text-sm font-medium mb-4">Variations</h4>
                                <VariationsManager
                                    type={flag.type === 2 ? 'JSON' : 'String'}
                                    variations={variations}
                                    onChange={setVariations}
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
    );
}
