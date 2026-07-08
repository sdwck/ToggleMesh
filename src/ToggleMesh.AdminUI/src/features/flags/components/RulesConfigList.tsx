import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { FormControl, FormField, FormItem } from '@/components/ui/form';
import { useFieldArray } from 'react-hook-form';
import type { Control, UseFormReturn } from 'react-hook-form';

interface RulesConfigListProps {
    form: UseFormReturn<any>;
    control: Control<any>;
    operators: string[];
    isLoadingOperators: boolean;
    name?: string;
    canEditEnv: boolean;
    disabled?: boolean;
    emptyMessage?: string;
    showInSegmentSpecialHandling?: boolean;
}

export function RulesConfigList({ 
    form, 
    control, 
    operators, 
    isLoadingOperators, 
    name = 'rules', 
    canEditEnv,
    disabled = false,
    emptyMessage = "No rules defined.",
    showInSegmentSpecialHandling = false
}: RulesConfigListProps) {
    const { fields, append, remove } = useFieldArray({
        control,
        name: name,
    });

    const addConditionOR = () => {
        const maxGroupId = fields.length > 0 ? Math.max(...fields.map((f: any) => f.groupId)) : -1;
        append({ groupId: maxGroupId + 1, attribute: '', operator: 'Equals', value: '' });
    };

    const addRuleAND = (groupId: number) => {
        append({ groupId, attribute: '', operator: 'Equals', value: '' });
    };

    const groupedRules = fields.reduce((acc, field: any, index) => {
        const groupId = form.getValues(`${name}.${index}.groupId`);
        if (groupId === undefined) return acc;
        if (!acc[groupId]) acc[groupId] = [];
        acc[groupId].push({ ...field, index });
        return acc;
    }, {} as Record<number, any[]>);

    const isFullyDisabled = !canEditEnv || disabled;

    return (
        <div className="space-y-4">
            <div className="flex justify-between items-center">
                <div className="text-sm font-medium">Targeting Rules</div>
                {!isFullyDisabled && (
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
                                <div className="absolute -top-[21px] left-6 bg-background px-2 text-xs font-semibold text-muted-foreground z-10 border border-border/40 rounded">
                                    OR
                                </div>
                            )}
                            <div className="px-6 pt-6 pb-6 border border-border/40 rounded-lg space-y-4 bg-muted/10 relative">
                                {groupFields.map((field, i) => (
                                    <div key={field.id} className="relative">
                                        {i > 0 && (
                                            <div className="absolute -top-4 left-6 bg-background px-1 text-[10px] font-medium text-muted-foreground z-10">
                                                AND
                                            </div>
                                        )}
                                        <div className="flex flex-col sm:flex-row sm:items-start gap-3 relative pr-8 sm:pr-0">
                                            {showInSegmentSpecialHandling && form.watch(`${name}.${field.index}.operator`) === 'InSegment' ? (
                                                <div className="flex-1 w-full flex items-center h-9 px-3 border border-border/40 rounded-md bg-muted/30 text-sm text-muted-foreground shadow-sm">
                                                    User Context
                                                </div>
                                            ) : (
                                                <div className="flex-1 w-full space-y-2">
                                                    <FormField
                                                        control={control}
                                                        name={`${name}.${field.index}.attribute`}
                                                        render={({ field: inputField }) => (
                                                            <FormItem>
                                                                <FormControl>
                                                                    <Input
                                                                        placeholder="Attribute (e.g. Email)"
                                                                        disabled={isFullyDisabled}
                                                                        onKeyDown={(e) => {
                                                                            if (e.key === 'Enter') e.preventDefault();
                                                                        }}
                                                                        {...inputField}
                                                                    />
                                                                </FormControl>
                                                            </FormItem>
                                                        )}
                                                    />
                                                </div>
                                            )}

                                            <div className="w-full sm:w-[180px]">
                                                <FormField
                                                    control={control}
                                                    name={`${name}.${field.index}.operator`}
                                                    render={({ field: selectField }) => (
                                                        <FormItem>
                                                            <Select
                                                                disabled={isFullyDisabled || isLoadingOperators}
                                                                onValueChange={selectField.onChange}
                                                                value={selectField.value}
                                                            >
                                                                <FormControl>
                                                                    <SelectTrigger>
                                                                        <SelectValue placeholder="Operator" />
                                                                    </SelectTrigger>
                                                                </FormControl>
                                                                <SelectContent>
                                                                    {operators.map((op) => (
                                                                        <SelectItem key={op} value={op}>
                                                                            {op}
                                                                        </SelectItem>
                                                                    ))}
                                                                </SelectContent>
                                                            </Select>
                                                        </FormItem>
                                                    )}
                                                />
                                            </div>

                                            <div className="flex-1 w-full space-y-2">
                                                <FormField
                                                    control={control}
                                                    name={`${name}.${field.index}.value`}
                                                    render={({ field: inputField }) => (
                                                        <FormItem>
                                                            <FormControl>
                                                                <Input
                                                                    placeholder="Value"
                                                                    disabled={isFullyDisabled}
                                                                    onKeyDown={(e) => {
                                                                        if (e.key === 'Enter') e.preventDefault();
                                                                    }}
                                                                    {...inputField}
                                                                />
                                                            </FormControl>
                                                        </FormItem>
                                                    )}
                                                />
                                            </div>

                                            {!isFullyDisabled && (
                                                <Button
                                                    type="button"
                                                    variant="ghost"
                                                    size="icon"
                                                    className="text-muted-foreground hover:text-destructive shrink-0 absolute right-0 top-0 sm:static"
                                                    onClick={() => remove(field.index)}
                                                >
                                                    <Trash2 className="h-4 w-4" />
                                                </Button>
                                            )}
                                        </div>
                                        
                                        {(form.formState.errors as any)?.[name]?.[field.index] && (
                                            <p className="text-xs text-destructive mt-1">Please fill all fields.</p>
                                        )}
                                    </div>
                                ))}
                                
                                {!isFullyDisabled && (
                                    <div className="pt-2">
                                        <Button
                                            type="button"
                                            variant="outline"
                                            size="sm"
                                            onClick={() => addRuleAND(groupId)}
                                        >
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
                        {emptyMessage}
                    </div>
                )}
            </div>
        </div>
    );
}
