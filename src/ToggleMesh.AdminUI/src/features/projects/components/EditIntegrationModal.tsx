import { useEffect } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';
import { Form, FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { toast } from 'sonner';
import { useUpdateIntegration } from '@/api/queries';
import { type Integration } from '@/api/types';
import { handleApiError } from '@/api/errorUtils';
import { EventSelectionCheckboxes } from './EventSelectionCheckboxes';

const editIntegrationSchema = z.object({
    name: z.string().min(1, 'Name is required'),
    events: z.array(z.string()).min(1, 'At least one event must be selected'),
    environmentIds: z.array(z.string()),
    isActive: z.boolean()
});

type EditIntegrationValues = z.infer<typeof editIntegrationSchema>;

interface EditIntegrationModalProps {
    isOpen: boolean;
    onClose: () => void;
    integration: Integration;
    projectId: string;
}

export function EditIntegrationModal({ isOpen, onClose, integration, projectId }: EditIntegrationModalProps) {
    const updateIntegration = useUpdateIntegration(projectId);

    const form = useForm<EditIntegrationValues>({
        resolver: zodResolver(editIntegrationSchema),
        defaultValues: {
            name: integration.name,
            events: integration.events,
            environmentIds: integration.environmentIds,
            isActive: integration.isActive
        },
    });

    useEffect(() => {
        if (isOpen) {
            form.reset({
                name: integration.name,
                events: integration.events,
                environmentIds: integration.environmentIds,
                isActive: integration.isActive
            });
        }
    }, [isOpen, integration, form]);

    const onSubmit = async (values: EditIntegrationValues) => {
        try {
            await updateIntegration.mutateAsync({
                id: integration.id,
                ...values
            });
            toast.success('Integration updated');
            onClose();
        } catch (error) {
            handleApiError(error, form.setError);
        }
    };


    return (
        <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="sm:max-w-[500px]">
                <DialogHeader>
                    <DialogTitle>Edit Integration</DialogTitle>
                    <DialogDescription>
                        Update settings for this {integration.provider} integration. Note: webhook URL cannot be changed after creation.
                    </DialogDescription>
                </DialogHeader>

                <Form {...form}>
                    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                        <FormField
                            control={form.control}
                            name="isActive"
                            render={({ field }) => (
                                <FormItem className="flex flex-row items-center space-x-3 space-y-0 rounded-md border p-4">
                                    <FormControl>
                                        <Checkbox
                                            checked={field.value}
                                            onCheckedChange={field.onChange}
                                            className="mt-0"
                                        />
                                    </FormControl>
                                    <div className="space-y-1 leading-none pt-0.5">
                                        <label className="text-sm font-medium">
                                            Active
                                        </label>
                                        <p className="text-sm text-muted-foreground">
                                            Whether this integration should receive events.
                                        </p>
                                    </div>
                                </FormItem>
                            )}
                        />

                        <FormField
                            control={form.control}
                            name="name"
                            render={({ field }) => (
                                <FormItem>
                                    <div className="text-sm font-medium">Name</div>
                                    <FormControl>
                                        <Input {...field} />
                                    </FormControl>
                                    <FormMessage />
                                </FormItem>
                            )}
                        />

                        <EventSelectionCheckboxes form={form} name="events" />

                        <DialogFooter className="mt-6">
                            <Button type="button" variant="outline" onClick={onClose}>
                                Cancel
                            </Button>
                            <Button type="submit" disabled={updateIntegration.isPending}>
                                {updateIntegration.isPending ? 'Saving...' : 'Save Changes'}
                            </Button>
                        </DialogFooter>
                    </form>
                </Form>
            </DialogContent>
        </Dialog>
    );
}
