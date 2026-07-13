import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import type { SegmentDto } from '@/api/types';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Form, FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { useRuleOperators } from '@/api/queries';
import { ruleSchema } from '../../flags/validation';
import { RulesConfigList } from '../../flags/components/RulesConfigList';

const formSchema = z.object({
    name: z.string().min(1, 'Name is required'),
    description: z.string().optional(),
    rules: z.array(ruleSchema),
});

type FormValues = z.infer<typeof formSchema>;

interface SegmentEditorDialogProps {
    segment: SegmentDto | null;
    open: boolean;
    onOpenChange: (open: boolean) => void;
    onSave: (data: FormValues) => Promise<void>;
    isSaving: boolean;
    mode: 'create' | 'edit' | 'view';
}

export function SegmentEditorDialog({ segment, open, onOpenChange, onSave, isSaving, mode }: SegmentEditorDialogProps) {
    const { data: dynamicOperators, isLoading: isLoadingOperators } = useRuleOperators();
    const operators = (dynamicOperators || []).filter(op => op !== 'InSegment' && op !== 'IN_SEGMENT');

    const form = useForm<FormValues>({
        resolver: zodResolver(formSchema) as any,
        defaultValues: {
            name: '',
            description: '',
            rules: [],
        },
    });

    useEffect(() => {
        if (!open) {
            return;
        }

        if ((mode === 'edit' || mode === 'view') && segment) {
            form.reset({
                name: segment.name,
                description: segment.description || '',
                rules: segment.rules || [],
            });
        } else if (mode === 'create') {
            form.reset({
                name: '',
                description: '',
                rules: [],
            });
        }
    }, [segment, open, mode, form]);

    const onSubmit = form.handleSubmit(async (data) => {
        await onSave(data);
    });

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto">
                <DialogHeader>
                    <DialogTitle>
                        {mode === 'create' ? 'Create Segment' : mode === 'edit' ? 'Edit Segment' : 'View Segment'}
                    </DialogTitle>
                    <DialogDescription>
                        {mode === 'view' ? 'View targeting rules for this segment.' : 'Define targeting rules for this segment. Users matching these rules will be included.'}
                    </DialogDescription>
                </DialogHeader>

                <Form {...form}>
                    <form onSubmit={form.handleSubmit(onSubmit as any)} className="space-y-6">
                        <div className="space-y-4">
                            <FormField
                                control={form.control as any}
                                name="name"
                                render={({ field }) => (
                                    <FormItem>
                                        <FormControl>
                                            <Input placeholder="Segment Name" disabled={mode === 'view'} {...field} />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />
                            
                            <FormField
                                control={form.control as any}
                                name="description"
                                render={({ field }) => (
                                    <FormItem>
                                        <FormControl>
                                            <Input placeholder="Description (optional)" disabled={mode === 'view'} {...field} />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />

                            <RulesConfigList 
                                form={form as any}
                                control={form.control as any}
                                operators={operators}
                                isLoadingOperators={isLoadingOperators}
                                variations={[]}
                                canEditEnv={mode !== 'view'}
                                disabled={mode === 'view'}
                                emptyMessage="No rules defined. This segment will match everyone."
                            />
                        </div>

                        <DialogFooter className="px-6 py-4 border-t border-border/40 shrink-0 bg-zinc-950">
                            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{mode === 'view' ? 'Close' : 'Cancel'}</Button>
                            {mode !== 'view' && (
                                <Button type="submit" disabled={isSaving}>
                                    {isSaving ? 'Saving...' : 'Save Segment'}
                                </Button>
                            )}
                        </DialogFooter>
                    </form>
                </Form>
            </DialogContent>
        </Dialog>
    );
}
