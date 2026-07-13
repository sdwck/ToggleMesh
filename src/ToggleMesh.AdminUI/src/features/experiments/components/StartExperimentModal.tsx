import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Play, AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { Slider } from '@/components/ui/slider';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from "@/components/ui/dialog";
import {
    Form,
    FormControl,
    FormField,
    FormItem,
    FormLabel,
    FormMessage,
} from '@/components/ui/form';
import { useUniqueEvents, useAnalyticsSchema } from '@/api/queries';

const formSchema = z.object({
    mode: z.enum(['classic', 'mab']),
    goalEvent: z.string().min(1, 'Goal event is required'),
    optimizationType: z.union([z.literal(0), z.literal(1)]),
    contextPartitionKeys: z.array(z.string()),
    mabExplorationFloor: z.number().min(0).max(10).optional(),
    balanceWeights: z.boolean().optional(),
});

type FormValues = z.infer<typeof formSchema>;

interface Props {
    projectId: string;
    envId: string;
    flagKey: string;
    currentRolloutPercentage: number | null;
    isLoading: boolean;
    rulesCount?: number;
    onStart: (values: FormValues) => void;
    canEditEnv?: boolean;
    variationsCount?: number;
}

export function StartExperimentModal({ projectId, envId, flagKey, currentRolloutPercentage, isLoading, rulesCount = 0, onStart, canEditEnv = true, variationsCount = 2 }: Props) {
    const [open, setOpen] = useState(false);
    const { data: uniqueEvents } = useUniqueEvents(projectId, envId);

    const form = useForm<FormValues>({
        resolver: zodResolver(formSchema),
        defaultValues: {
            mode: 'classic',
            goalEvent: '',
            optimizationType: 0,
            contextPartitionKeys: [],
            mabExplorationFloor: 5,
            balanceWeights: false
        },
    });

    const isMab = form.watch('mode') === 'mab';
    const goalEvent = form.watch('goalEvent');
    const isEvenlySplit = currentRolloutPercentage !== null && Math.abs(currentRolloutPercentage - (10000 / variationsCount)) <= 1;

    const { data: schema } = useAnalyticsSchema(projectId, envId, flagKey, goalEvent);

    const handleSubmit = (values: FormValues) => {
        onStart(values);
        setOpen(false);
    };

    const showMabWarning = isMab && !isEvenlySplit && !form.watch('balanceWeights');

    return (
        <Dialog open={open} onOpenChange={setOpen}>
            <DialogTrigger asChild>
                <Button
                    type="button"
                    size="sm"
                    className="bg-emerald-500 hover:bg-emerald-600 text-white"
                    disabled={isLoading || !canEditEnv}
                >
                    {isLoading ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <Play className="h-4 w-4 mr-2" />}
                    {isLoading ? 'Starting...' : 'Start New Experiment'}
                </Button>
            </DialogTrigger>
            <DialogContent className="sm:max-w-[500px] bg-zinc-950 border-border/40 max-h-[90vh] overflow-y-auto">
                <DialogHeader>
                    <DialogTitle>Start Experiment</DialogTitle>
                    <DialogDescription>
                        Configure your experiment. This will clear existing metrics and start tracking fresh data for A/B testing.
                    </DialogDescription>
                </DialogHeader>

                <Form {...form}>
                    <form 
                        onSubmit={(e) => {
                            e.stopPropagation();
                            form.handleSubmit(handleSubmit)(e);
                        }} 
                        className="space-y-6 py-4"
                    >
                        <div className="space-y-4">
                            <FormField
                                control={form.control}
                                name="mode"
                                render={({ field }) => (
                                    <FormItem className="flex items-center justify-between border border-border/40 p-4 rounded-lg bg-muted/10 space-y-0">
                                        <div className="space-y-0.5">
                                            <FormLabel>Enable AI Auto-Rollout (MAB)</FormLabel>
                                            <p className="text-[11px] text-muted-foreground">
                                                Automatically shifts traffic to the winning variant.
                                            </p>
                                        </div>
                                        <FormControl>
                                            <Switch
                                                checked={field.value === 'mab'}
                                                onCheckedChange={(val) => field.onChange(val ? 'mab' : 'classic')}
                                            />
                                        </FormControl>
                                    </FormItem>
                                )}
                            />

                            {isMab && (
                                <FormField
                                    control={form.control}
                                    name="mabExplorationFloor"
                                    render={({ field }) => (
                                        <FormItem className="space-y-4 p-4 border border-border/40 rounded-lg bg-muted/10">
                                            <div className="flex justify-between items-center">
                                                <FormLabel className="text-sm font-medium">Exploration Floor</FormLabel>
                                                <span className="text-sm font-medium">{field.value}%</span>
                                            </div>
                                            <FormControl>
                                                <Slider
                                                    min={0}
                                                    max={10}
                                                    step={1}
                                                    value={[field.value || 0]}
                                                    onValueChange={vals => field.onChange(vals[0])}
                                                    className="py-2"
                                                />
                                            </FormControl>
                                            <p className="text-[11px] text-muted-foreground mt-2">
                                                Minimum traffic percentage allocated to each variation.
                                            </p>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                            )}

                            <FormField
                                control={form.control}
                                name="goalEvent"
                                render={({ field }) => (
                                    <FormItem>
                                        <FormLabel className="text-xs text-muted-foreground">Primary Metric (Goal)</FormLabel>
                                        <FormControl>
                                            <div className="relative">
                                                <Input 
                                                    list="unique-events-list" 
                                                    {...field} 
                                                    className="bg-zinc-950 border-zinc-800 w-full"
                                                    placeholder="Enter event name (e.g. checkout_completed)" 
                                                />
                                                <datalist id="unique-events-list">
                                                    {uniqueEvents?.map(ev => (
                                                        <option key={ev} value={ev} />
                                                    ))}
                                                </datalist>
                                                {(!uniqueEvents || uniqueEvents.length === 0) && !field.value && (
                                                    <div className="absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none">
                                                        <span className="text-xs text-zinc-500">No recent events</span>
                                                    </div>
                                                )}
                                            </div>
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />

                            {goalEvent && (
                                <div className="space-y-4 pt-4 border-t border-border/40">
                                    {isMab && showMabWarning && (
                                        <div className="p-4 rounded-lg bg-amber-500/10 border border-amber-500/30 space-y-3">
                                            <h4 className="text-amber-500 text-sm font-semibold flex items-center gap-2">
                                                <AlertTriangle className="h-4 w-4" />
                                                Slower Learning Phase
                                            </h4>
                                            <div className="text-xs text-amber-500/90 space-y-2">
                                                {currentRolloutPercentage !== null && currentRolloutPercentage !== 10000 / variationsCount && (
                                                    <p>Current traffic split is not evenly balanced across variations.</p>
                                                )}
                                                {rulesCount > 0 && (
                                                    <div className="flex items-start gap-2 text-[13px] text-amber-500 bg-amber-500/10 p-3 rounded-md border border-amber-500/20">
                                                        <AlertTriangle className="h-4 w-4 shrink-0 mt-0.5" />
                                                        <p>
                                                            <strong>Warning:</strong> You have <strong>{rulesCount} targeting rules</strong> with custom weights. 
                                                            "Reset to Even Split" will only balance the <em>Default Rollout</em>. This might skew your experiment traffic if users hit the targeting rules instead. We recommend balancing your rules manually or deleting them before starting.
                                                        </p>
                                                    </div>
                                                )}
                                            </div>
                                            <p className="text-xs text-amber-500/80">
                                                Starting with an unbalanced split will delay MAB convergence. The algorithm needs traffic on all variants to learn efficiently.
                                            </p>
                                            <Button
                                                type="button"
                                                variant="outline"
                                                size="sm"
                                                className="w-full bg-zinc-950 border-amber-500/50 hover:bg-amber-500/20 text-amber-500"
                                                onClick={() => {
                                                    form.setValue('balanceWeights', true);
                                                }}
                                            >
                                                Reset to {variationsCount === 2 ? '50/50' : 'Even Split'} (Recommended)
                                            </Button>
                                        </div>
                                    )}

                                    {!isMab && rulesCount === 0 && !isEvenlySplit && !form.watch('balanceWeights') && (
                                        <div className="p-4 rounded-lg bg-amber-500/10 border border-amber-500/30 space-y-3">
                                            <h4 className="text-amber-500 text-sm font-semibold flex items-center gap-2">
                                                <AlertTriangle className="h-4 w-4" />
                                                No Traffic Splitting Configured
                                            </h4>
                                            <p className="text-xs text-amber-500/90">
                                                This flag has no targeting rules and no percentage rollout. The experiment will not split traffic (100% of users will receive the same variant).
                                            </p>
                                            <Button
                                                type="button"
                                                variant="outline"
                                                size="sm"
                                                className="w-full bg-zinc-950 border-amber-500/50 hover:bg-amber-500/20 text-amber-500"
                                                onClick={() => {
                                                    form.setValue('balanceWeights', true);
                                                }}
                                            >
                                                Set {variationsCount === 2 ? '50/50' : 'Even'} Rollout
                                            </Button>
                                        </div>
                                    )}

                                    <FormField
                                        control={form.control}
                                        name="optimizationType"
                                        render={({ field }) => (
                                            <FormItem>
                                                <FormLabel className="text-xs text-muted-foreground">Optimization Type</FormLabel>
                                                <FormControl>
                                                    <Select
                                                        value={field.value.toString()}
                                                        onValueChange={(v) => field.onChange(parseInt(v) as 0 | 1)}
                                                    >
                                                        <SelectTrigger>
                                                            <SelectValue placeholder="Optimization Type" />
                                                        </SelectTrigger>
                                                        <SelectContent>
                                                            <SelectItem value="0">Conversion Rate</SelectItem>
                                                            <SelectItem value="1">Revenue / Value</SelectItem>
                                                        </SelectContent>
                                                    </Select>
                                                </FormControl>
                                                {field.value === 1 && !schema?.hasValue && (
                                                    <p className="text-[10px] text-amber-500 mt-1">
                                                        Warning: No 'Value' detected for this event in the last 30 days.
                                                    </p>
                                                )}
                                                <FormMessage />
                                            </FormItem>
                                        )}
                                    />

                                    <FormField
                                        control={form.control}
                                        name="contextPartitionKeys"
                                        render={({ field }) => (
                                            <FormItem>
                                                <FormLabel className="text-xs text-muted-foreground">Contextual Partitions</FormLabel>
                                                <div className="flex gap-2">
                                                    <FormControl>
                                                        <Input
                                                            placeholder="e.g. Country"
                                                            id="context-partition-input"
                                                            list="context-keys-list"
                                                            onKeyDown={(e) => {
                                                                if (e.key === 'Enter') {
                                                                    e.preventDefault();
                                                                    const val = e.currentTarget.value.trim();
                                                                    if (val) {
                                                                        const keys = field.value;
                                                                        if (!keys.includes(val)) field.onChange([...keys, val]);
                                                                        e.currentTarget.value = '';
                                                                    }
                                                                }
                                                            }}
                                                        />
                                                    </FormControl>
                                                    <datalist id="context-keys-list">
                                                        {schema?.contextKeys?.map(key => (
                                                            <option key={key} value={key} />
                                                        ))}
                                                    </datalist>
                                                    <Button
                                                        type="button"
                                                        variant="secondary"
                                                        onClick={() => {
                                                            const el = document.getElementById('context-partition-input') as HTMLInputElement;
                                                            const val = el.value.trim();
                                                            if (val) {
                                                                const keys = field.value;
                                                                if (!keys.includes(val)) field.onChange([...keys, val]);
                                                                el.value = '';
                                                            }
                                                        }}
                                                    >Add</Button>
                                                </div>
                                                <div className="flex flex-wrap gap-2 mt-2">
                                                    {field.value.map(key => (
                                                        <span key={key} className="inline-flex items-center gap-1 px-2 py-1 rounded-md bg-muted text-xs">
                                                            {key}
                                                            <button
                                                                type="button"
                                                                className="hover:text-foreground"
                                                                onClick={() => {
                                                                    field.onChange(field.value.filter((k: string) => k !== key));
                                                                }}
                                                            >&times;</button>
                                                        </span>
                                                    ))}
                                                </div>
                                                <FormMessage />
                                            </FormItem>
                                        )}
                                    />
                                </div>
                            )}
                        </div>
                        <DialogFooter>
                            <Button type="submit" disabled={isLoading} className="w-full">
                                Start Experiment
                            </Button>
                        </DialogFooter>
                    </form>
                </Form>
            </DialogContent>
        </Dialog>
    );
}
