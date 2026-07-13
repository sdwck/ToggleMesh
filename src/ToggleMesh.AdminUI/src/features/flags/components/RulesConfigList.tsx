import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { FormControl, FormField, FormItem } from '@/components/ui/form';
import { useFieldArray } from 'react-hook-form';
import type { Control, UseFormReturn } from 'react-hook-form';
import { RolloutConfig } from './RolloutConfig';
import { getDefaultRollout } from '../utils';

interface RulesConfigListProps {
    form: UseFormReturn<any>;
    control: Control<any>;
    operators: string[];
    isLoadingOperators: boolean;
    variations: { id: string; value: string }[];
    name?: string;
    canEditEnv: boolean;
    disabled?: boolean;
    emptyMessage?: string;
    showInSegmentSpecialHandling?: boolean;
    type?: number;
}

export function RulesConfigList({
    form,
    control,
    operators,
    isLoadingOperators,
    variations,
    name = 'rules',
    canEditEnv,
    disabled = false,
    emptyMessage = "No rules defined.",
    showInSegmentSpecialHandling = false,
    type
}: RulesConfigListProps) {
    const { fields, append, remove } = useFieldArray({
        control,
        name: name,
    });

    const defaultRollout = getDefaultRollout(variations);

    const getNextGroupId = () => {
        const currentRules = form.getValues(name) || [];
        const maxId = currentRules.reduce((max: number, rule: any) => Math.max(max, Number(rule.groupId) || 0), 0);
        return maxId + 1;
    };

    const addConditionOR = () => {
        append({ priority: 0, groupId: getNextGroupId(), attribute: '', operator: 'Equals', value: '', rollout: defaultRollout });
    };

    const addRuleAND = (groupId: number) => {
        append({ priority: 0, groupId, attribute: '', operator: 'Equals', value: '', rollout: defaultRollout });
    };

    const groupedRules = fields.reduce((acc, field: any, index) => {
        const groupId = form.getValues(`${name}.${index}.groupId`);
        if (!groupId) return acc;
        if (!acc[groupId]) acc[groupId] = [];
        acc[groupId].push({ ...field, index });
        return acc;
    }, {} as Record<string, any[]>);

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
                {Object.entries(groupedRules).map(([groupId, groupFields], groupIndex) => {
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

                                            <div className="flex gap-2 w-full flex-col sm:flex-row sm:w-auto flex-1 items-start">
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
                                                        className="text-muted-foreground hover:text-destructive shrink-0 self-end sm:self-start mt-2 sm:mt-0"
                                                        onClick={() => remove(field.index)}
                                                        disabled={isFullyDisabled}
                                                    >
                                                        <Trash2 className="h-4 w-4" />
                                                    </Button>
                                                )}
                                            </div>
                                        </div>

                                        {(form.formState.errors as any)?.[name]?.[field.index] && (
                                            <p className="text-xs text-destructive mt-1">Please fill all fields.</p>
                                        )}
                                    </div>
                                ))}

                                <div className="pt-2 border-t border-border/40 space-y-4">
                                    <FormField
                                        control={control}
                                        name={`${name}.${groupFields[0].index}.rollout`}
                                        render={({ field }) => (
                                            <FormItem>
                                                <RolloutConfig
                                                    type={type}
                                                    variations={variations}
                                                    rollout={field.value || []}
                                                    onChange={field.onChange}
                                                    disabled={isFullyDisabled}
                                                />
                                            </FormItem>
                                        )}
                                    />
                                    <div className="flex justify-between items-center">
                                        <div className="flex gap-2">
                                            {!isFullyDisabled && (
                                                <Button type="button" variant="secondary" size="sm" onClick={() => addRuleAND(Number(groupId))}>
                                                    <Plus className="mr-2 h-4 w-4" /> Add Condition (AND)
                                                </Button>
                                            )}
                                        </div>
                                        {!isFullyDisabled && (
                                            <Button
                                                type="button"
                                                variant="ghost"
                                                size="sm"
                                                className="text-muted-foreground hover:text-destructive"
                                                onClick={() => remove(groupFields.map((f: any) => f.index))}
                                            >
                                                <Trash2 className="mr-2 h-4 w-4" /> Delete Block
                                            </Button>
                                        )}
                                    </div>
                                </div>
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
