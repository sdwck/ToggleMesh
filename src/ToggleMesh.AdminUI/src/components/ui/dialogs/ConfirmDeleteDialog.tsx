import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { Form, FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { AlertTriangle } from 'lucide-react';
import { useEffect } from 'react';

interface ConfirmDeleteDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    title: string;
    description?: string;
    entityName: string;
    onConfirm: () => Promise<void>;
}

export function ConfirmDeleteDialog({ open, onOpenChange, title, description, entityName, onConfirm }: ConfirmDeleteDialogProps) {
    const confirmDeleteSchema = z.object({
        confirmName: z.string().min(1, 'Confirmation is required'),
    });
    
    type DeleteValues = z.infer<typeof confirmDeleteSchema>;
    
    const form = useForm<DeleteValues>({
        resolver: zodResolver(confirmDeleteSchema),
        defaultValues: { confirmName: '' }
    });

    useEffect(() => {
        if (!open) form.reset();
    }, [open, form]);

    const onSubmit = async (values: DeleteValues) => {
        if (values.confirmName !== entityName) {
            form.setError('confirmName', { message: 'Name does not match' });
            return;
        }
        await onConfirm();
        onOpenChange(false);
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle className="text-rose-500 flex items-center gap-2">
                        <AlertTriangle className="h-5 w-5" />
                        {title}
                    </DialogTitle>
                    <DialogDescription>
                        {description || `This action cannot be undone. This will permanently delete "${entityName}".`}
                        <br /><br />
                        Please type <strong>{entityName}</strong> to confirm.
                    </DialogDescription>
                </DialogHeader>
                <Form {...form}>
                    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                        <FormField
                            control={form.control}
                            name="confirmName"
                            render={({ field }) => (
                                <FormItem>
                                    <FormControl>
                                        <Input placeholder={entityName} {...field} />
                                    </FormControl>
                                    <FormMessage />
                                </FormItem>
                            )}
                        />
                        <DialogFooter>
                            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={form.formState.isSubmitting}>
                                Cancel
                            </Button>
                            <Button type="submit" variant="destructive" disabled={form.formState.isSubmitting || form.watch('confirmName') !== entityName}>
                                {form.formState.isSubmitting ? 'Deleting...' : 'Delete'}
                            </Button>
                        </DialogFooter>
                    </form>
                </Form>
            </DialogContent>
        </Dialog>
    );
}
