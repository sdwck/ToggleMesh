import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { useCreateOrganization } from '@/api/queries';
import { useOrganizationStore } from '@/stores/useOrganizationStore';
import { handleApiError } from '@/api/errorUtils';
import { toast } from 'sonner';

const createOrgSchema = z.object({
    name: z.string().min(1, 'Organization name is required'),
});

export type CreateOrgValues = z.infer<typeof createOrgSchema>;

interface CreateOrganizationDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    onSuccess?: (orgId: string) => void;
}

export function CreateOrganizationDialog({ open, onOpenChange, onSuccess }: CreateOrganizationDialogProps) {
    const createOrganization = useCreateOrganization();
    const { setActiveOrganizationId } = useOrganizationStore();

    const orgForm = useForm<CreateOrgValues>({
        resolver: zodResolver(createOrgSchema),
        defaultValues: { name: '' },
    });

    useEffect(() => {
        if (open) {
            orgForm.reset({ name: '' });
        }
    }, [open, orgForm]);

    const handleCreateOrg = async (values: CreateOrgValues) => {
        try {
            const newOrg = await createOrganization.mutateAsync(values);
            onOpenChange(false);
            toast.success('Organization created');
            if (newOrg?.id) {
                setActiveOrganizationId(newOrg.id);
                if (onSuccess) onSuccess(newOrg.id);
            }
        } catch (error: any) {
            handleApiError(error, orgForm.setError, 'Failed to create organization');
        }
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="border-border/40 bg-zinc-950">
                <DialogHeader>
                    <DialogTitle>Create Organization</DialogTitle>
                    <DialogDescription>
                        An organization contains your projects and team members.
                    </DialogDescription>
                </DialogHeader>
                <Form {...orgForm}>
                    <form onSubmit={orgForm.handleSubmit(handleCreateOrg)} className="space-y-4 py-4">
                        <FormField
                            control={orgForm.control}
                            name="name"
                            render={({ field }) => (
                                <FormItem>
                                    <FormLabel>Organization Name</FormLabel>
                                    <FormControl>
                                        <Input
                                            placeholder="e.g., Acme Corp"
                                            {...field}
                                            autoFocus
                                        />
                                    </FormControl>
                                    <FormMessage />
                                </FormItem>
                            )}
                        />
                        {orgForm.formState.errors.root && (
                            <div className="text-sm text-destructive font-medium">
                                {orgForm.formState.errors.root.message}
                            </div>
                        )}
                        <DialogFooter>
                            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
                            <Button type="submit" disabled={createOrganization.isPending}>
                                {createOrganization.isPending ? 'Creating...' : 'Create'}
                            </Button>
                        </DialogFooter>
                    </form>
                </Form>
            </DialogContent>
        </Dialog>
    );
}
