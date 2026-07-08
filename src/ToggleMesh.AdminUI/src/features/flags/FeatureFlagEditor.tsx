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
import { Switch } from '@/components/ui/switch';
import { Slider } from '@/components/ui/slider';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from 'sonner';
import { useUpdateFeatureFlag, useRuleOperators } from '@/api/queries';
import { ExperimentResults } from '../experiments/components/ExperimentResults';
import { SimulationModal } from '../experiments/components/SimulationModal';
import { SegmentEditorDialog } from '../environments/components/SegmentEditorDialog';
import { useCreateSegment } from '@/api/queries';

import { ruleSchema } from './validation';
import { RulesConfigList } from './components/RulesConfigList';

const formSchema = z.object({
    isEnabled: z.boolean(),
    rolloutPercentage: z.number().nullable(),
    isRolloutEnabled: z.boolean(),
    rules: z.array(ruleSchema),
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
        resolver: zodResolver(formSchema),
        defaultValues: {
            isEnabled: false,
            isRolloutEnabled: false,
            rolloutPercentage: 0,
            rules: [],
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
                isEnabled: flag.isEnabled,
                isRolloutEnabled: flag.rolloutPercentage !== null,
                rolloutPercentage: flag.rolloutPercentage || 0,
                rules: flag.rules || [],
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
                isEnabled: values.isEnabled,
                rolloutPercentage: values.isRolloutEnabled ? values.rolloutPercentage : null,
                rules: values.rules,
            });
            toast.success('Feature flag updated');
            onOpenChange(false);
        } catch {
            toast.error('Failed to update feature flag');
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
                        <Button variant="outline" size="sm" className="gap-2 border-emerald-500/30 text-emerald-500 hover:bg-emerald-500/10 mt-0 self-end" onClick={() => setSimOpen(true)}>
                            Simulate Traffic
                        </Button>
                    )}
                </SheetHeader>

                <SimulationModal
                    open={simOpen}
                    onOpenChange={setSimOpen}
                    projectId={projectId}
                    envId={envId}
                    flagKey={flag.key}
                    eventName={flag.mabGoalEvent || "test_event"}
                />

                <Tabs defaultValue={defaultTab} className="w-full mt-6">
                    <TabsList className="grid w-full grid-cols-2 bg-zinc-900/50">
                        <TabsTrigger value="rules">Targeting Rules</TabsTrigger>
                        <TabsTrigger value="experiments">A/B Testing</TabsTrigger>
                    </TabsList>

                    <TabsContent value="rules" className="mt-4">
                        {flag.isExperimentActive && (
                            <div className="mb-6 p-4 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-sm flex items-start gap-3">
                                <Lock className="h-5 w-5 text-emerald-500 shrink-0 mt-0.5" />
                                <div>
                                    <h4 className="font-medium text-emerald-400 mb-1">Locked by Active Experiment</h4>
                                    <p className="text-emerald-500/80">Targeting rules and rollout percentages are currently being managed by an active A/B test. Stop the experiment to make manual changes.</p>
                                </div>
                            </div>
                        )}
                        <Form {...form}>
                            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-8">
                                <div className="flex items-center justify-between px-6 py-4 border border-border/40 rounded-lg bg-muted/20">
                                    <div className="space-y-0.5">
                                        <Label className="text-base">Enable Flag</Label>
                                        <p className="text-sm text-muted-foreground">Serve this flag to users.</p>
                                    </div>
                                    <Switch
                                        checked={form.watch('isEnabled')}
                                        onCheckedChange={(val) => form.setValue('isEnabled', val)}
                                        disabled={flag.isExperimentActive || !canEditEnv}
                                    />
                                </div>

                                <div className="space-y-4 px-2">
                                    <div className="flex items-center gap-2">
                                        <Switch
                                            checked={form.watch('isRolloutEnabled')}
                                            onCheckedChange={(val) => {
                                                form.setValue('isRolloutEnabled', val);
                                            }}
                                            disabled={flag.isExperimentActive || !canEditEnv}
                                        />
                                        <Label>Incremental Rollout</Label>
                                    </div>

                                    {form.watch('isRolloutEnabled') && (
                                        <div className="pl-12 pr-4 space-y-4">
                                            <div className="flex items-center justify-between">
                                                <span className="text-sm text-muted-foreground">Percentage</span>
                                                <div className="flex items-center text-sm font-medium">
                                                    <input
                                                        type="number"
                                                        {...form.register('rolloutPercentage', { valueAsNumber: true })}
                                                        disabled={flag.isExperimentActive || !canEditEnv}
                                                        className={`w-[4ch] bg-transparent outline-none border-b border-dashed border-primary/40 hover:border-primary/80 focus:border-primary transition-colors text-center appearance-none [&::-webkit-inner-spin-button]:appearance-none ${flag.isExperimentActive || !canEditEnv ? 'cursor-not-allowed opacity-50' : 'cursor-text'}`}
                                                        onBlur={(e) => {
                                                            let val = parseInt(e.target.value || '0', 10);
                                                            if (isNaN(val)) val = 0;
                                                            val = Math.max(0, Math.min(100, val));
                                                            form.setValue('rolloutPercentage', val);
                                                        }}
                                                        onKeyDown={(e) => {
                                                            if (e.key === 'Enter') {
                                                                e.preventDefault();
                                                                e.currentTarget.blur();
                                                            }
                                                        }}
                                                    />
                                                    <span>%</span>
                                                </div>
                                            </div>
                                            <Slider
                                                value={[form.watch('rolloutPercentage') || 0]}
                                                max={100}
                                                step={1}
                                                disabled={flag.isExperimentActive || !canEditEnv}
                                                onValueChange={(val) => form.setValue('rolloutPercentage', val[0])}
                                            />
                                        </div>
                                    )}
                                </div>

                                <Separator className="bg-border/40" />

                                <div className="space-y-4 px-2">
                                    <RulesConfigList
                                        form={form}
                                        control={form.control as any}
                                        operators={operators}
                                        isLoadingOperators={isLoadingOperators}
                                        canEditEnv={canEditEnv && !flag.isExperimentActive}
                                        disabled={flag.isExperimentActive}
                                        emptyMessage="No targeting rules defined. The flag will be served based on the rollout percentage."
                                        showInSegmentSpecialHandling={true}
                                    />
                                </div>

                                <SheetFooter className="mt-8 pt-4 border-t border-border/40">
                                    <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
                                    <Button type="submit" disabled={updateFlag.isPending || flag.isExperimentActive || !canEditEnv}>
                                        {updateFlag.isPending ? 'Saving...' : 'Save Changes'}
                                    </Button>
                                </SheetFooter>
                            </form>
                        </Form>
                    </TabsContent>

                    <TabsContent value="experiments" className="mt-4">
                        <ExperimentResults
                            projectId={projectId}
                            envId={envId}
                            flagKey={flag.key}
                            mabGoalEvent={flag.mabGoalEvent}
                            highlightTrack={searchParams.get('track')}
                            isExperimentActive={flag.isExperimentActive}
                            isMabEnabled={flag.isMabEnabled}
                            mabOptimizationType={flag.mabOptimizationType}
                            contextPartitionKeys={flag.contextPartitionKeys}
                            rolloutPercentage={flag.rolloutPercentage ?? undefined}
                            hasRules={(flag.rules?.length ?? 0) > 0}
                            canEditEnv={canEditEnv}
                        />
                    </TabsContent>
                </Tabs>
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