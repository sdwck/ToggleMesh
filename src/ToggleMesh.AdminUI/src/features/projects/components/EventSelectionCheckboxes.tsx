import { type UseFormReturn } from 'react-hook-form';
import { FormField, FormItem, FormControl, FormMessage } from '@/components/ui/form';
import { Checkbox } from '@/components/ui/checkbox';

export const availableEvents = [
    { id: 'flag.created', label: 'Flag Created' },
    { id: 'flag.updated', label: 'Flag Updated' },
    { id: 'flag.deleted', label: 'Flag Deleted' },
    { id: 'experiment.started', label: 'Experiment Started' },
    { id: 'experiment.stopped', label: 'Experiment Stopped' },
    { id: 'experiment.srm_detected', label: 'SRM Detected' },
    { id: 'experiment.winner_found', label: 'Winner Found' },
    { id: 'experiment.degraded', label: 'Experiment Degraded' }
];

interface EventSelectionCheckboxesProps {
    form: UseFormReturn<any>;
    name: string;
    title?: string;
}

export function EventSelectionCheckboxes({ form, name, title = "Events to Trigger On" }: EventSelectionCheckboxesProps) {
    return (
        <div className="space-y-2">
            <h4 className="text-sm font-medium">{title}</h4>
            <FormField
                control={form.control}
                name={name}
                render={() => (
                    <FormItem>
                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                            {availableEvents.map(evt => (
                                <FormField
                                    key={evt.id}
                                    control={form.control}
                                    name={name}
                                    render={({ field }) => {
                                        return (
                                            <FormItem
                                                key={evt.id}
                                                className="flex flex-row items-center space-x-2 space-y-0"
                                            >
                                                <FormControl>
                                                    <Checkbox
                                                        checked={field.value?.includes(evt.id)}
                                                        onCheckedChange={(checked) => {
                                                            return checked
                                                                ? field.onChange([...(field.value || []), evt.id])
                                                                : field.onChange(
                                                                    field.value?.filter(
                                                                        (value: string) => value !== evt.id
                                                                    )
                                                                )
                                                        }}
                                                    />
                                                </FormControl>
                                                <div className="space-y-1 leading-none pt-0.5">
                                                    <label className="text-sm font-normal cursor-pointer leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">
                                                        {evt.label}
                                                    </label>
                                                </div>
                                            </FormItem>
                                        )
                                    }}
                                />
                            ))}
                        </div>
                        <FormMessage />
                    </FormItem>
                )}
            />
        </div>
    );
}
