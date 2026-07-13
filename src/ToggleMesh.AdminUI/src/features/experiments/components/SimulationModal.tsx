import { useState, useEffect, useRef } from "react";
import api from "@/api/axios";
import { useQueryClient } from "@tanstack/react-query";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Slider } from "@/components/ui/slider";
import { toast } from "sonner";
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, useFieldArray } from 'react-hook-form';
import * as z from 'zod';
import {
    Form,
    FormControl,
    FormField,
    FormItem,
    FormLabel,
    FormMessage,
} from '@/components/ui/form';
import type { FeatureFlag } from "@/api/types";

const variantSchema = z.object({
    variationId: z.string(),
    label: z.string(),
    cr: z.number().min(0.1).max(100.0),
    rev: z.number().min(0).max(1000.0)
});

const simulationSchema = z.object({
    participants: z.number().min(1000).max(500000),
    eventName: z.string().optional(),
    contextProperties: z.string().optional(),
    variants: z.array(variantSchema)
});
type SimulationValues = z.infer<typeof simulationSchema>;

interface SimulationModalProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    projectId: string;
    envId: string;
    flag: FeatureFlag;
}

export function SimulationModal({ open, onOpenChange, projectId, envId, flag }: SimulationModalProps) {
    const [isLoading, setIsLoading] = useState(false);
    const abortControllerRef = useRef<AbortController | null>(null);
    const queryClient = useQueryClient();

    const form = useForm<SimulationValues>({
        resolver: zodResolver(simulationSchema),
        defaultValues: {
            participants: 10000,
            eventName: flag.mabGoalEvent || "test_event",
            contextProperties: '{\n  "country": ["US", "CA", "GB", "AU"]\n}',
            variants: flag.variations?.map((v, i) => ({
                variationId: v.id,
                label: v.value || `Variation ${i + 1}`,
                cr: i === 0 ? 10.0 : 12.0,
                rev: i === 0 ? 20.0 : 25.0
            })) || []
        }
    });

    const { fields } = useFieldArray({
        control: form.control,
        name: "variants"
    });

    useEffect(() => {
        if (open && flag.variations) {
            form.reset({
                participants: 10000,
                eventName: flag.mabGoalEvent || "test_event",
                contextProperties: '{\n  "country": ["US", "CA", "GB", "AU"]\n}',
                variants: flag.variations.map((v, i) => ({
                    variationId: v.id,
                    label: v.value || `Variation ${i + 1}`,
                    cr: i === 0 ? 10.0 : 12.0 + (i * 2.5),
                    rev: i === 0 ? 20.0 : 25.0 + (i * 5.0)
                }))
            });
        }
    }, [open, flag.variations, form]);

    const handleSimulate = async (values: SimulationValues) => {
        setIsLoading(true);
        abortControllerRef.current = new AbortController();
        try {
            const reqVariations = values.variants.map(v => ({
                variationId: v.variationId,
                conversionRate: v.cr / 100,
                value: v.rev > 0 ? v.rev : null
            }));

            let parsedProps = {};
            if (values.contextProperties) {
                try {
                    parsedProps = JSON.parse(values.contextProperties);
                } catch (e) {
                    toast.error("Invalid JSON in context properties");
                    setIsLoading(false);
                    return;
                }
            }

            await api.post(`/projects/${projectId}/environments/${envId}/flags/${flag.key}/experiments/simulate`, {
                eventName: values.eventName || flag.mabGoalEvent || "test_event",
                participantsCount: values.participants,
                variations: reqVariations,
                contextProperties: parsedProps
            }, {
                signal: abortControllerRef.current.signal
            });

            toast.success(`Successfully injected ${values.participants * values.variants.length} users!`, {
                description: "The background worker will aggregate these metrics in a few seconds."
            });
            onOpenChange(false);

            setTimeout(() => queryClient.invalidateQueries(), 1000);
            setTimeout(() => queryClient.invalidateQueries(), 3000);
            setTimeout(() => queryClient.invalidateQueries(), 6000);
        } catch (err: any) {
            if (err.name === 'CanceledError' || err.code === 'ERR_CANCELED') {
                toast.info("Simulation canceled");
            } else {
                toast.error(err.message || "Failed to simulate traffic");
            }
        } finally {
            setIsLoading(false);
            abortControllerRef.current = null;
        }
    };

    const handleCancel = () => {
        if (abortControllerRef.current) {
            abortControllerRef.current.abort();
        }
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="sm:max-w-[425px] bg-zinc-950 border-border/40 max-h-[90vh] overflow-y-auto">
                <DialogHeader>
                    <DialogTitle>Simulate Traffic (Dev Only)</DialogTitle>
                    <DialogDescription>
                        Instantly inject synthetic traffic to test the Bayesian algorithm without waiting.
                    </DialogDescription>
                </DialogHeader>

                <Form {...form}>
                    <form
                        onSubmit={(e) => {
                            e.stopPropagation();
                            form.handleSubmit(handleSimulate)(e);
                        }}
                    >
                        <div className="space-y-6 py-4">
                            <FormField
                                control={form.control}
                                name="participants"
                                render={({ field }) => (
                                    <FormItem>
                                        <div className="flex justify-between items-center mb-3">
                                            <FormLabel>Users per Variant</FormLabel>
                                            <span className="text-xs text-muted-foreground font-mono">{field.value.toLocaleString()}</span>
                                        </div>
                                        <FormControl>
                                            <Slider
                                                value={[field.value]}
                                                min={1000}
                                                max={500000}
                                                step={1000}
                                                onValueChange={v => field.onChange(v[0])}
                                            />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />

                            <FormField
                                control={form.control}
                                name="eventName"
                                render={({ field }) => (
                                    <FormItem>
                                        <div className="flex justify-between items-center mb-1">
                                            <FormLabel>Event Name</FormLabel>
                                        </div>
                                        <FormControl>
                                            <input
                                                type="text"
                                                className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
                                                placeholder="Goal event name"
                                                {...field}
                                            />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />

                            <FormField
                                control={form.control}
                                name="contextProperties"
                                render={({ field }) => (
                                    <FormItem>
                                        <div className="flex justify-between items-center mb-1">
                                            <FormLabel>Context Properties (JSON)</FormLabel>
                                        </div>
                                        <FormControl>
                                            <textarea
                                                className="flex min-h-[80px] w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50 font-mono"
                                                placeholder='{"country": ["US", "CA"]}'
                                                {...field}
                                            />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />

                            {fields.map((field, index) => (
                                <div key={field.id} className="p-4 rounded-lg border border-border/40 bg-zinc-900/30 space-y-4">
                                    <h4 className="font-medium text-sm text-zinc-300">
                                        Variation: <span className="font-mono text-emerald-400 ml-1">{field.label}</span>
                                    </h4>

                                    <FormField
                                        control={form.control}
                                        name={`variants.${index}.cr`}
                                        render={({ field: crField }) => (
                                            <FormItem>
                                                <div className="flex justify-between items-center mb-2">
                                                    <FormLabel className="text-xs text-muted-foreground">Conversion Rate</FormLabel>
                                                    <span className="text-xs font-mono">{crField.value.toFixed(1)}%</span>
                                                </div>
                                                <FormControl>
                                                    <Slider
                                                        value={[crField.value]}
                                                        min={0.1}
                                                        max={100.0}
                                                        step={0.1}
                                                        onValueChange={v => crField.onChange(v[0])}
                                                    />
                                                </FormControl>
                                                <FormMessage />
                                            </FormItem>
                                        )}
                                    />

                                    <FormField
                                        control={form.control}
                                        name={`variants.${index}.rev`}
                                        render={({ field: revField }) => (
                                            <FormItem>
                                                <div className="flex justify-between items-center mb-2">
                                                    <FormLabel className="text-xs text-muted-foreground">Revenue Value ($)</FormLabel>
                                                    <span className="text-xs font-mono">${revField.value.toFixed(1)}</span>
                                                </div>
                                                <FormControl>
                                                    <Slider
                                                        value={[revField.value]}
                                                        min={0}
                                                        max={1000.0}
                                                        step={1.0}
                                                        onValueChange={v => revField.onChange(v[0])}
                                                    />
                                                </FormControl>
                                                <FormMessage />
                                            </FormItem>
                                        )}
                                    />
                                </div>
                            ))}
                        </div>

                        <DialogFooter>
                            {isLoading ? (
                                <Button type="button" variant="destructive" onClick={handleCancel}>
                                    Stop Simulation
                                </Button>
                            ) : (
                                <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
                            )}
                            <Button type="submit" disabled={isLoading} className="bg-emerald-600 hover:bg-emerald-700 text-white">
                                {isLoading ? "Injecting..." : "Blast Traffic"}
                            </Button>
                        </DialogFooter>
                    </form>
                </Form>
            </DialogContent>
        </Dialog>
    );
}
