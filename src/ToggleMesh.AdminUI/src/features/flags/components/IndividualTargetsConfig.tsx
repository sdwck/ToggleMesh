import { useFieldArray } from 'react-hook-form';
import type { UseFormReturn } from 'react-hook-form';
import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

interface IndividualTargetsConfigProps {
    form: UseFormReturn<any>;
    disabled?: boolean;
    variations: { id: string; value: string }[];
}

export function IndividualTargetsConfig({ form, disabled, variations }: IndividualTargetsConfigProps) {
    const { fields, append, remove } = useFieldArray({
        control: form.control,
        name: "individualTargets"
    });

    return (
        <div className="space-y-4 px-2">
            <div className="flex items-center gap-2 mb-2">
                <Label className="text-base font-semibold">Individual Targets</Label>
            </div>
            <p className="text-sm text-muted-foreground mb-4">
                Override variations for specific identities (e.g., user IDs, session IDs). Targets are evaluated before any rules.
            </p>

            <div className="space-y-3">
                {fields.map((field, index) => {
                    const individualErrors = form.formState.errors.individualTargets as any;
                    const variationError = individualErrors?.[index]?.variationId?.message;
                    const keyError = individualErrors?.[index]?.key?.message;

                    return (
                        <div key={field.id} className="flex items-start gap-3 p-3 bg-zinc-900/40 border border-border/50 rounded-lg">
                            <div className="flex-1 space-y-1">
                                <Input
                                    {...form.register(`individualTargets.${index}.key`)}
                                    placeholder="Identity Key (e.g. user-123)"
                                    disabled={disabled}
                                    className="h-9 bg-zinc-900 border-zinc-800"
                                />
                                {keyError && <p className="text-xs text-red-500 mt-1">{keyError as string}</p>}
                            </div>

                            <div className="w-[200px] shrink-0 space-y-1">
                                <Select
                                    disabled={disabled}
                                    value={form.watch(`individualTargets.${index}.variationId`)}
                                    onValueChange={(val) => form.setValue(`individualTargets.${index}.variationId`, val)}
                                >
                                    <SelectTrigger className="h-9 bg-zinc-900 border-zinc-800">
                                        <SelectValue placeholder="Select Variation" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {variations.map(v => (
                                            <SelectItem key={v.id} value={v.id}>{v.value}</SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                {variationError && <p className="text-xs text-red-500 mt-1">{variationError as string}</p>}
                            </div>

                            <Button
                                type="button"
                                variant="ghost"
                                size="icon"
                                className="h-9 w-9 text-muted-foreground hover:text-red-400 hover:bg-red-500/10 shrink-0"
                                onClick={() => remove(index)}
                                disabled={disabled}
                            >
                                <Trash2 className="h-4 w-4" />
                            </Button>
                        </div>
                    );
                })}

                {fields.length === 0 && (
                    <div className="text-sm text-muted-foreground italic p-4 border border-dashed border-border/50 rounded-lg bg-zinc-900/20 text-center">
                        No individual targets defined.
                    </div>
                )}
            </div>

            <Button
                type="button"
                variant="outline"
                size="sm"
                className="mt-4 border-dashed"
                onClick={() => append({ key: '', variationId: '' })}
                disabled={disabled}
            >
                <Plus className="h-4 w-4 mr-2" />
                Add Target
            </Button>
        </div>
    );
}
