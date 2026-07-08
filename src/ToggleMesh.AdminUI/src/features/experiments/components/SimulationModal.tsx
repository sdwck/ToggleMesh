import { useState } from "react";
import api from "@/api/axios";
import { useQueryClient } from "@tanstack/react-query";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Slider } from "@/components/ui/slider";
import { toast } from "sonner";
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import {
    Form,
    FormControl,
    FormField,
    FormItem,
    FormLabel,
    FormMessage,
} from '@/components/ui/form';

const simulationSchema = z.object({
    participants: z.number().min(1000).max(500000),
    controlCR: z.number().min(0.1).max(50.0),
    treatmentCR: z.number().min(0.1).max(50.0),
    controlValue: z.number().min(0).max(200.0),
    treatmentValue: z.number().min(0).max(200.0)
});
type SimulationValues = z.infer<typeof simulationSchema>;

interface SimulationModalProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    projectId: string;
    envId: string;
    flagKey: string;
    eventName: string;
}

export function SimulationModal({ open, onOpenChange, projectId, envId, flagKey, eventName }: SimulationModalProps) {
    const [isLoading, setIsLoading] = useState(false);
    const queryClient = useQueryClient();

    const form = useForm<SimulationValues>({
        resolver: zodResolver(simulationSchema),
        defaultValues: {
            participants: 100000,
            controlCR: 10.0,
            treatmentCR: 12.0,
            controlValue: 20.0,
            treatmentValue: 25.0
        }
    });

    const handleSimulate = async (values: SimulationValues) => {
        setIsLoading(true);
        try {
            await api.post(`/projects/${projectId}/environments/${envId}/flags/${flagKey}/experiments/simulate`, {
                eventName,
                participantsCount: values.participants,
                controlConversionRate: values.controlCR / 100,
                treatmentConversionRate: values.treatmentCR / 100,
                controlValue: values.controlValue > 0 ? values.controlValue : null,
                treatmentValue: values.treatmentValue > 0 ? values.treatmentValue : null,
            });

            toast.success(`Successfully injected ${values.participants * 2} users!`, {
                description: "The background worker will aggregate these metrics in a few seconds."
            });
            onOpenChange(false);

            setTimeout(() => queryClient.invalidateQueries(), 1000);
            setTimeout(() => queryClient.invalidateQueries(), 3000);
            setTimeout(() => queryClient.invalidateQueries(), 6000);
        } catch (err: any) {
            toast.error(err.message || "Failed to simulate traffic");
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="sm:max-w-[425px] bg-zinc-950 border-border/40">
                <DialogHeader>
                    <DialogTitle>Simulate Traffic (Dev Only)</DialogTitle>
                    <DialogDescription>
                        Instantly inject synthetic traffic to test the Bayesian algorithm without waiting.
                    </DialogDescription>
                </DialogHeader>

                <Form {...form}>
                    <form onSubmit={form.handleSubmit(handleSimulate)}>
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
                                name="controlCR"
                                render={({ field }) => (
                                    <FormItem>
                                        <div className="flex justify-between items-center mb-3">
                                            <FormLabel>Control Conversion Rate</FormLabel>
                                            <span className="text-xs text-muted-foreground font-mono">{field.value.toFixed(1)}%</span>
                                        </div>
                                        <FormControl>
                                            <Slider
                                                value={[field.value]}
                                                min={0.1}
                                                max={50.0}
                                                step={0.1}
                                                onValueChange={v => field.onChange(v[0])}
                                                className="[&_[role=slider]]:border-muted-foreground"
                                            />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />

                            <FormField
                                control={form.control}
                                name="treatmentCR"
                                render={({ field }) => (
                                    <FormItem>
                                        <div className="flex justify-between items-center mb-3">
                                            <FormLabel>Treatment Conversion Rate</FormLabel>
                                            <span className="text-xs text-emerald-500 font-mono">{field.value.toFixed(1)}%</span>
                                        </div>
                                        <FormControl>
                                            <Slider
                                                value={[field.value]}
                                                min={0.1}
                                                max={50.0}
                                                step={0.1}
                                                onValueChange={v => field.onChange(v[0])}
                                                className="[&_[role=slider]]:border-emerald-500"
                                            />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />

                            <FormField
                                control={form.control}
                                name="controlValue"
                                render={({ field }) => (
                                    <FormItem>
                                        <div className="flex justify-between items-center mb-3">
                                            <FormLabel>Control Revenue Value ($)</FormLabel>
                                            <span className="text-xs text-muted-foreground font-mono">${field.value.toFixed(1)}</span>
                                        </div>
                                        <FormControl>
                                            <Slider
                                                value={[field.value]}
                                                min={0}
                                                max={200.0}
                                                step={1.0}
                                                onValueChange={v => field.onChange(v[0])}
                                                className="[&_[role=slider]]:border-muted-foreground"
                                            />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />

                            <FormField
                                control={form.control}
                                name="treatmentValue"
                                render={({ field }) => (
                                    <FormItem>
                                        <div className="flex justify-between items-center mb-3">
                                            <FormLabel>Treatment Revenue Value ($)</FormLabel>
                                            <span className="text-xs text-emerald-500 font-mono">${field.value.toFixed(1)}</span>
                                        </div>
                                        <FormControl>
                                            <Slider
                                                value={[field.value]}
                                                min={0}
                                                max={200.0}
                                                step={1.0}
                                                onValueChange={v => field.onChange(v[0])}
                                                className="[&_[role=slider]]:border-emerald-500"
                                            />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />
                        </div>

                        <DialogFooter>
                            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isLoading}>Cancel</Button>
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
