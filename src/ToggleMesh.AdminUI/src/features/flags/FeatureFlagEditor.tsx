import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Lock } from 'lucide-react';
import type { FeatureFlag } from '@/api/types';
import { Form } from '@/components/ui/form';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription, SheetFooter } from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from 'sonner';
import { useUpdateFeatureFlag, useRuleOperators } from '@/api/queries';
import { ExperimentResults } from '../experiments/components/ExperimentResults';
import { SimulationModal } from '../experiments/components/SimulationModal';
import { SegmentEditorDialog } from '../environments/components/SegmentEditorDialog';
import { useCreateSegment } from '@/api/queries';
import { handleApiError } from '@/api/errorUtils';
import { ruleSchema } from './validation';
import { RulesConfigList } from './components/RulesConfigList';
import { RolloutConfig } from './components/RolloutConfig';
import { IndividualTargetsConfig } from './components/IndividualTargetsConfig';

const formSchema = z.object({
    fallthroughRollout: z.array(z.object({
        variationId: z.string(),
        weight: z.number()
    })),
    rules: z.array(ruleSchema),
    type: z.number().default(0),
    variations: z.array(z.object({
        id: z.string(),
        value: z.string()
    })).optional(),
    individualTargets: z.array(z.object({
        key: z.string().min(1, "Identity key is required"),
        variationId: z.string().min(1, "Variation is required")
    })).optional()
}).superRefine((val, ctx) => {
    if (val.type !== 0 && val.variations) {
        val.variations.forEach((v, idx) => {
            if (!v.value.trim()) {
                ctx.addIssue({
                    code: z.ZodIssueCode.custom,
                    message: "Variation value cannot be empty",
                    path: ['variations', idx, 'value']
                });
                return;
            }
            if (val.type === 2) {
                try {
                    JSON.parse(v.value);
                } catch (e) {
                    ctx.addIssue({
                        code: z.ZodIssueCode.custom,
                        message: "Invalid JSON format",
                        path: ['variations', idx, 'value']
                    });
                }
            }
        });
    }
});

type FormValues = z.infer<typeof formSchema>;

interface FeatureFlagEditorProps {
    flag: FeatureFlag | null;
    projectId: string;
    envId: string;
    open: boolean;
    onOpenChange: (open: boolean) => void;
    canEditEnv?: boolean;
}

export function FeatureFlagEditor({ flag, projectId, envId, open, onOpenChange, canEditEnv = true }: FeatureFlagEditorProps) {
    const updateFlag = useUpdateFeatureFlag(projectId, envId, flag?.key || '');

    const { data: dynamicOperators, isLoading: isLoadingOperators } = useRuleOperators();
    const operators = ['InSegment', ...(dynamicOperators || []).filter(op => op !== 'InSegment')];

    const form = useForm<FormValues>({
        resolver: zodResolver(formSchema) as any,
        defaultValues: {
            fallthroughRollout: [],
            rules: [],
            type: 0,
            variations: [],
            individualTargets: []
        },
    });

    const [formLoadedForFlag, setFormLoadedForFlag] = useState<string | null>(null);
    const [simOpen, setSimOpen] = useState(false);

    useEffect(() => {
        if (!open) {
            setFormLoadedForFlag(null);
            return;
        }

        if (flag && open && formLoadedForFlag !== flag.key) {
            form.reset({
                fallthroughRollout: (flag.fallthroughRollout || []).map(r => ({ ...r, weight: r.weight / 100 })),
                rules: (flag.rules || []).map(rule => ({
                    ...rule,
                    rollout: (rule.rollout || []).map(r => ({ ...r, weight: r.weight / 100 }))
                })),
                type: flag.type,
                variations: flag.variations || [],
                individualTargets: flag.individualTargets ? Object.entries(flag.individualTargets).map(([k, v]) => ({ key: k, variationId: v })) : []
            });
            setFormLoadedForFlag(flag.key);
        }
    }, [flag, open, form, formLoadedForFlag]);

    const [isCreateSegmentOpen, setIsCreateSegmentOpen] = useState(false);
    const createSegment = useCreateSegment();

    const handleCreateSegment = async (data: any) => {
        try {
            await createSegment.mutateAsync({
                projectId,
                environmentId: envId,
                data: { name: data.name, description: data.description, rules: data.rules }
            });
            setIsCreateSegmentOpen(false);
            toast.success('Segment created successfully');
        } catch {
            toast.error('Failed to create segment');
        }
    };

    const onSubmit = async (values: FormValues) => {
        if (!flag) return;
        try {
            await updateFlag.mutateAsync({
                fallthroughRollout: values.fallthroughRollout.map(r => ({ ...r, weight: Math.round(r.weight * 100) })),
                rules: values.rules.map(rule => ({
                    ...rule,
                    rollout: (rule.rollout || []).map(r => ({ ...r, weight: Math.round(r.weight * 100) }))
                })),
                individualTargets: values.individualTargets?.reduce((acc, curr) => {
                    acc[curr.key] = curr.variationId;
                    return acc;
                }, {} as Record<string, string>)
            });
            toast.success('Feature flag updated');
            onOpenChange(false);
        } catch (error: any) {
            handleApiError(error, form.setError as any, 'Failed to update feature flag');
            if (error?.response?.data?.errors) {
                const errData = error.response.data.errors;
                if (Array.isArray(errData)) {
                    errData.forEach((e: any) => toast.error(e.message || e.reason || 'Validation error'));
                } else {
                    Object.values(errData).forEach((e: any) => {
                        const msg = Array.isArray(e) ? e[0] : e;
                        toast.error(String(msg));
                    });
                }
            }
        }
    };

    const [searchParams] = window.location.search ? [new URLSearchParams(window.location.search)] : [new URLSearchParams()];
    const defaultTab = searchParams.get('tab') || 'rules';

    const handleOpenChange = (isOpen: boolean) => {
        onOpenChange(isOpen);
        if (!isOpen) {
            setTimeout(() => {
                document.body.style.pointerEvents = '';
            }, 100);
        }
    };

    if (!flag) return null;

    return (
        <Sheet open={open} onOpenChange={handleOpenChange}>
            <SheetContent className="w-full sm:max-w-2xl overflow-y-auto bg-zinc-950">
                <SheetHeader className="flex flex-row items-start justify-between space-y-0 pb-2">
                    <div className="space-y-1.5">
                        <SheetTitle className="font-mono">{flag.key}</SheetTitle>
                        <SheetDescription>Configure targeting rules and rollout strategy.</SheetDescription>
                    </div>
                    {import.meta.env.DEV && (
                        <Button type="button" variant="outline" size="sm" className="gap-2 border-emerald-500/30 text-emerald-500 hover:bg-emerald-500/10 mt-0 self-end" onClick={() => setSimOpen(true)}>
                            Simulate Traffic
                        </Button>
                    )}
                </SheetHeader>

                <SimulationModal
                    open={simOpen}
                    onOpenChange={setSimOpen}
                    projectId={projectId}
                    envId={envId}
                    flag={flag}
                />

                <Form {...form}>
                    <form onSubmit={form.handleSubmit(onSubmit as any)} className="pb-10">
                        <Tabs defaultValue={defaultTab} className="w-full mt-6">
                            <TabsList className="grid w-full bg-zinc-900/50 grid-cols-2">
                                <TabsTrigger value="rules">Targeting Rules</TabsTrigger>
                                <TabsTrigger value="experiments">A/B Testing</TabsTrigger>
                            </TabsList>

                            <TabsContent value="rules" className="mt-4 space-y-8">
                                {flag.isExperimentActive && (
                                    <div className="mb-6 p-4 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-sm flex items-start gap-3">
                                        <Lock className="h-5 w-5 text-emerald-500 shrink-0 mt-0.5" />
                                        <div>
                                            <h4 className="font-medium text-emerald-400 mb-1">Locked by Active Experiment</h4>
                                            <p className="text-emerald-500/80">Targeting rules and rollout percentages are currently being managed by an active A/B test. Stop the experiment to make manual changes.</p>
                                        </div>
                                    </div>
                                )}

                                <div className="space-y-4 px-2">
                                    <div className="flex items-center gap-2 mb-2">
                                        <Label className="text-base font-semibold">Default Rollout</Label>
                                    </div>
                                    <p className="text-sm text-muted-foreground mb-4">
                                        Served to users if no targeting rules match.
                                    </p>
                                    <RolloutConfig
                                        type={flag.type}
                                        variations={flag.variations || []}
                                        rollout={form.watch('fallthroughRollout')}
                                        onChange={(val) => form.setValue('fallthroughRollout', val)}
                                        disabled={flag.isExperimentActive || !canEditEnv}
                                    />
                                </div>

                                <Separator className="bg-border/40" />

                                <div className="space-y-4 px-2">
                                    <IndividualTargetsConfig
                                        form={form}
                                        disabled={!canEditEnv}
                                        variations={flag.variations || []}
                                    />
                                </div>

                                <Separator className="bg-border/40" />

                                <div className="space-y-4 px-2">
                                    <RulesConfigList
                                        form={form}
                                        control={form.control as any}
                                        operators={operators}
                                        isLoadingOperators={isLoadingOperators}
                                        variations={flag.variations || []}
                                        canEditEnv={canEditEnv && !flag.isExperimentActive}
                                        disabled={flag.isExperimentActive}
                                        emptyMessage="No targeting rules defined. The flag will be served based on the rollout percentage."
                                        showInSegmentSpecialHandling={true}
                                        type={flag.type}
                                    />
                                </div>

                                <SheetFooter className="mt-8 pt-4 border-t border-border/40">
                                    <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
                                    <Button type="submit" disabled={updateFlag.isPending || !canEditEnv}>
                                        {updateFlag.isPending ? 'Saving...' : 'Save Changes'}
                                    </Button>
                                </SheetFooter>
                            </TabsContent>



                            <TabsContent value="experiments" className="mt-4">
                                <ExperimentResults
                                    projectId={projectId}
                                    envId={envId}
                                    flagKey={flag.key}
                                    mabGoalEvent={flag.mabGoalEvent || null}
                                    highlightTrack={searchParams.get('track')}
                                    isExperimentActive={flag.isExperimentActive || false}
                                    isMabEnabled={flag.isMabEnabled || false}
                                    mabOptimizationType={flag.mabOptimizationType}
                                    contextPartitionKeys={flag.contextPartitionKeys}
                                    rolloutPercentage={flag.fallthroughRollout?.[0]?.weight ?? undefined}
                                    rulesCount={flag.rules?.length || 0}
                                    canEditEnv={canEditEnv}
                                />
                            </TabsContent>
                        </Tabs>
                    </form>
                </Form>
            </SheetContent>



            <SegmentEditorDialog
                open={isCreateSegmentOpen}
                onOpenChange={setIsCreateSegmentOpen}
                mode="create"
                segment={null}
                onSave={handleCreateSegment}
                isSaving={createSegment.isPending}
            />
        </Sheet>
    );
}