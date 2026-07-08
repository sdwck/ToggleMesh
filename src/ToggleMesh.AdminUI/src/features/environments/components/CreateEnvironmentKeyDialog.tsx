import { useState, useEffect } from 'react';
import { useCreateEnvironmentKey } from '@/api/queries';
import { KeyType } from '@/api/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { handleApiError } from '@/api/errorUtils';
import { Copy } from 'lucide-react';
import { toast } from 'sonner';

const createKeySchema = z.object({
    name: z.string().min(1, 'Key name is required'),
    type: z.nativeEnum(KeyType)
});
type CreateKeyValues = z.infer<typeof createKeySchema>;

interface CreateEnvironmentKeyDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    projectId: string;
    environmentId: string;
}

export function CreateEnvironmentKeyDialog({ open, onOpenChange, projectId, environmentId }: CreateEnvironmentKeyDialogProps) {
    const createKey = useCreateEnvironmentKey(projectId, environmentId);
    
    const [isRevealOpen, setIsRevealOpen] = useState(false);
    const [plainKeyRevealed, setPlainKeyRevealed] = useState('');

    const createForm = useForm<CreateKeyValues>({
        resolver: zodResolver(createKeySchema),
        defaultValues: { name: '', type: KeyType.Server }
    });

    useEffect(() => {
        if (open) {
            createForm.reset({ name: '', type: KeyType.Server });
        }
    }, [open, createForm]);

    const handleCreateKeySubmit = async (values: CreateKeyValues) => {
        try {
            const response = await createKey.mutateAsync({ name: values.name.trim(), type: values.type });
            onOpenChange(false);
            setPlainKeyRevealed(response.plainKey);
            setIsRevealOpen(true);
            toast.success('API Key created successfully');
        } catch (error: any) {
            handleApiError(error, createForm.setError, 'Failed to create API Key');
        }
    };

    const copyToClipboard = () => {
        if (plainKeyRevealed) {
            navigator.clipboard.writeText(plainKeyRevealed);
            toast.success('API Key copied to clipboard');
        }
    };

    return (
        <>
            <Dialog open={open} onOpenChange={onOpenChange}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle>Create API Key</DialogTitle>
                        <DialogDescription>
                            Create a new API key for this environment.
                        </DialogDescription>
                    </DialogHeader>
                    <Form {...createForm}>
                        <form onSubmit={createForm.handleSubmit(handleCreateKeySubmit)}>
                            <div className="space-y-4 py-4">
                                <FormField
                                    control={createForm.control}
                                    name="name"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Key Name</FormLabel>
                                            <FormControl>
                                                <Input {...field} placeholder="e.g. Production Backend" autoFocus />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                                <FormField
                                    control={createForm.control}
                                    name="type"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Key Type</FormLabel>
                                            <Select 
                                                value={field.value.toString()} 
                                                onValueChange={(val) => field.onChange(parseInt(val))}
                                            >
                                                <FormControl>
                                                    <SelectTrigger>
                                                        <SelectValue placeholder="Select type" />
                                                    </SelectTrigger>
                                                </FormControl>
                                                <SelectContent>
                                                    <SelectItem value={KeyType.Server.toString()}>Server (Backend)</SelectItem>
                                                    <SelectItem value={KeyType.Client.toString()}>Client (Frontend/Mobile)</SelectItem>
                                                </SelectContent>
                                            </Select>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                            </div>
                            <DialogFooter>
                                <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
                                <Button type="submit" disabled={createKey.isPending}>
                                    {createKey.isPending ? 'Creating...' : 'Create Key'}
                                </Button>
                            </DialogFooter>
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>

            <Dialog open={isRevealOpen} onOpenChange={(open) => {
                setIsRevealOpen(open);
                if (!open) setPlainKeyRevealed('');
            }}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle>API Key Created</DialogTitle>
                        <DialogDescription className="text-amber-500 font-medium">
                            Please copy your API key now. For security reasons, you will not be able to see it again!
                        </DialogDescription>
                    </DialogHeader>
                    <div className="py-6 flex items-center gap-2">
                        <Input value={plainKeyRevealed} readOnly className="font-mono bg-muted/40 text-sm" />
                        <Button variant="outline" size="icon" onClick={copyToClipboard} className="shrink-0">
                            <Copy className="h-4 w-4" />
                        </Button>
                    </div>
                    <DialogFooter>
                        <Button onClick={() => setIsRevealOpen(false)}>Done</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </>
    );
}
