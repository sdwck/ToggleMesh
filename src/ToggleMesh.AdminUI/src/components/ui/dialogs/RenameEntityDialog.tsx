import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { Form, FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { useEffect } from 'react';

interface RenameEntityDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    title: string;
    description?: string;
    currentName: string;
    onRename: (newName: string) => Promise<void>;
}

export function RenameEntityDialog({ open, onOpenChange, title, description, currentName, onRename }: RenameEntityDialogProps) {
    const renameSchema = z.object({
        name: z.string().min(1, 'Name is required'),
    });
    
    type RenameValues = z.infer<typeof renameSchema>;
    
    const form = useForm<RenameValues>({
        resolver: zodResolver(renameSchema),
        defaultValues: { name: currentName }
    });

    useEffect(() => {
        if (open) {
            form.reset({ name: currentName });
        }
    }, [open, currentName, form]);

    const onSubmit = async (values: RenameValues) => {
        if (values.name === currentName) {
            onOpenChange(false);
            return;
        }
        await onRename(values.name);
        onOpenChange(false);
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>{title}</DialogTitle>
                    {description && <DialogDescription>{description}</DialogDescription>}
                </DialogHeader>
                <Form {...form}>
                    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                        <FormField
                            control={form.control}
                            name="name"
                            render={({ field }) => (
                                <FormItem>
                                    <FormControl>
                                        <Input placeholder="Enter new name" {...field} />
                                    </FormControl>
                                    <FormMessage />
                                </FormItem>
                            )}
                        />
                        <DialogFooter>
                            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={form.formState.isSubmitting}>
                                Cancel
                            </Button>
                            <Button type="submit" disabled={form.formState.isSubmitting || form.watch('name') === currentName}>
                                {form.formState.isSubmitting ? 'Saving...' : 'Save'}
                            </Button>
                        </DialogFooter>
                    </form>
                </Form>
            </DialogContent>
        </Dialog>
    );
}
