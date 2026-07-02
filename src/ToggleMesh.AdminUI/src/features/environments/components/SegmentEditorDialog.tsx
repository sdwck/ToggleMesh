import { useEffect } from 'react';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Plus, Trash2, Settings2 } from 'lucide-react';
import type { SegmentDto } from '@/api/types';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useRuleOperators } from '@/api/queries';

const ruleSchema = z.object({
    groupId: z.number(),
    attribute: z.string().min(1, 'Attribute is required'),
    operator: z.string().min(1, 'Operator is required'),
    value: z.string().min(1, 'Value is required'),
});

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
    const operators = (dynamicOperators || []).filter(op => op !== 'IN_SEGMENT');

    const form = useForm<FormValues>({
        resolver: zodResolver(formSchema),
        defaultValues: {
            name: '',
            description: '',
            rules: [],
        },
    });

    const { fields, append, remove } = useFieldArray({
        control: form.control,
        name: 'rules',
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
    }, [segment, open, form, mode]);

    const onSubmit = async (values: FormValues) => {
        await onSave(values);
    };

    const addConditionOR = () => {
        const maxGroupId = fields.length > 0 ? Math.max(...fields.map(f => f.groupId)) : -1;
        append({ groupId: maxGroupId + 1, attribute: '', operator: 'Equals', value: '' });
    };

    const addRuleAND = (groupId: number) => {
        append({ groupId, attribute: '', operator: 'Equals', value: '' });
    };

    const groupedRules = fields.reduce((acc, field, index) => {
        if (!acc[field.groupId]) {
            acc[field.groupId] = [];
        }
        acc[field.groupId].push({ ...field, index });
        return acc;
    }, {} as Record<number, (typeof fields[0] & { index: number })[]>);

    const handleOpenChange = (isOpen: boolean) => {
        onOpenChange(isOpen);
        if (!isOpen) {
            setTimeout(() => {
                document.body.style.pointerEvents = '';
            }, 100);
        }
    };

    return (
        <Dialog open={open} onOpenChange={handleOpenChange}>
            <DialogContent className="max-w-3xl border-border/40 bg-zinc-950 p-0 gap-0 overflow-hidden flex flex-col max-h-[90vh]">
                <DialogHeader className="px-6 py-4 border-b border-border/40 shrink-0">
                    <DialogTitle>{mode === 'create' ? 'Create Segment' : mode === 'view' ? 'View Segment' : 'Edit Segment'}</DialogTitle>
                    <DialogDescription>
                        {mode === 'create' ? 'Define a new segment.' : mode === 'view' ? 'View segment details and rules.' : 'Update segment details and rules.'}
                    </DialogDescription>
                </DialogHeader>

                <form onSubmit={form.handleSubmit(onSubmit)} className="flex-1 overflow-y-auto px-6 py-4 space-y-6">
                    <div className="space-y-4">
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Name</label>
                            <Input
                                placeholder="e.g. Beta Users"
                                autoFocus={mode === 'create'}
                                {...form.register('name')}
                                disabled={mode === 'view'}
                                onKeyDown={(e) => {
                                    if (e.key === 'Enter') {
                                        e.preventDefault();
                                    }
                                }}
                            />
                            {form.formState.errors.name && (
                                <p className="text-xs text-destructive">{form.formState.errors.name.message}</p>
                            )}
                        </div>
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Description</label>
                            <Input
                                placeholder="Optional description"
                                {...form.register('description')}
                                disabled={mode === 'view'}
                                onKeyDown={(e) => {
                                    if (e.key === 'Enter') {
                                        e.preventDefault();
                                    }
                                }}
                            />
                        </div>
                    </div>

                    <div className="border-t border-border/40 pt-6">
                        <div className="flex items-center justify-between">
                            <div>
                                <h3 className="text-lg font-medium flex items-center gap-2">
                                    <Settings2 className="h-5 w-5" /> Segment Rules
                                </h3>
                                <p className="text-sm text-muted-foreground">Users matching these rules will be included in the segment.</p>
                            </div>
                            {mode !== 'view' && (
                                <Button type="button" variant="outline" size="sm" onClick={addConditionOR}>
                                    <Plus className="mr-2 h-4 w-4" /> Add Condition (OR)
                                </Button>
                            )}
                        </div>

                        <div className="space-y-6 mt-4">
                            {Object.entries(groupedRules).map(([groupIdStr, groupFields], groupIndex) => {
                                const groupId = parseInt(groupIdStr);
                                return (
                                    <div key={groupId} className="relative">
                                        {groupIndex > 0 && (
                                            <div className="absolute -top-[21px] left-6 bg-zinc-950 px-2 text-xs font-semibold text-muted-foreground z-10 border border-border/40 rounded">
                                                OR
                                            </div>
                                        )}
                                        <div className="px-6 pt-6 pb-6 border border-border/40 rounded-lg space-y-4 bg-muted/10 relative">
                                            {groupFields.map((field, i) => (
                                                <div key={field.id} className="relative">
                                                    {i > 0 && (
                                                        <div className="absolute -top-4 left-6 bg-zinc-950 px-1 text-[10px] font-medium text-muted-foreground z-10">
                                                            AND
                                                        </div>
                                                    )}
                                                    <div className="flex items-start gap-3">
                                                        <div className="flex-1 space-y-2">
                                                            <Input
                                                                placeholder="Attribute (e.g. Email)"
                                                                {...form.register(`rules.${field.index}.attribute`)}
                                                                disabled={mode === 'view'}
                                                                onKeyDown={(e) => {
                                                                    if (e.key === 'Enter') {
                                                                        e.preventDefault();
                                                                    }
                                                                }}
                                                            />
                                                        </div>
                                                        <div className="w-[180px]">
                                                            <Select
                                                                value={form.watch(`rules.${field.index}.operator`)}
                                                                onValueChange={(val) => {
                                                                    form.setValue(`rules.${field.index}.operator`, val);
                                                                }}
                                                                disabled={isLoadingOperators || mode === 'view'}
                                                            >
                                                                <SelectTrigger>
                                                                    <SelectValue placeholder="Operator" />
                                                                </SelectTrigger>
                                                                <SelectContent>
                                                                    {operators.map(op => (
                                                                        <SelectItem key={op} value={op}>{op}</SelectItem>
                                                                    ))}
                                                                </SelectContent>
                                                            </Select>
                                                        </div>
                                                        <div className="flex-1 space-y-2">
                                                            <Input
                                                                placeholder="Value"
                                                                {...form.register(`rules.${field.index}.value`)}
                                                                disabled={mode === 'view'}
                                                                onKeyDown={(e) => {
                                                                    if (e.key === 'Enter') {
                                                                        e.preventDefault();
                                                                    }
                                                                }}
                                                            />
                                                        </div>
                                                        {mode !== 'view' && (
                                                            <Button type="button" variant="ghost" size="icon" className="text-muted-foreground hover:text-destructive shrink-0" onClick={() => remove(field.index)}>
                                                                <Trash2 className="h-4 w-4" />
                                                            </Button>
                                                        )}
                                                    </div>
                                                    {form.formState.errors.rules?.[field.index] && (
                                                        <p className="text-xs text-destructive mt-1">Please fill all fields.</p>
                                                    )}
                                                </div>
                                            ))}
                                            {mode !== 'view' && (
                                                <div className="pt-2">
                                                    <Button type="button" variant="ghost" size="sm" className="text-xs" onClick={() => addRuleAND(groupId)}>
                                                        <Plus className="mr-1 h-3 w-3" /> Add Rule (AND)
                                                    </Button>
                                                </div>
                                            )}
                                        </div>
                                    </div>
                                );
                            })}

                            {fields.length === 0 && (
                                <div className="text-center p-8 border border-dashed border-border/40 rounded-lg text-muted-foreground text-sm">
                                    No rules defined. This segment will match everyone.
                                </div>
                            )}
                        </div>
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
            </DialogContent>
        </Dialog>
    );
}
