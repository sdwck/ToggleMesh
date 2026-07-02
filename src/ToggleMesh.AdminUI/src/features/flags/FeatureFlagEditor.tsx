import { useEffect, useState } from 'react';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Plus, Trash2, Settings2, Lock } from 'lucide-react';
import type { FeatureFlag } from '@/api/types';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription, SheetFooter } from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Slider } from '@/components/ui/slider';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from 'sonner';
import { useUpdateFeatureFlag, useRuleOperators, useSegments } from '@/api/queries';
import { ExperimentResults } from '../experiments/components/ExperimentResults';
import { SimulationModal } from '../experiments/components/SimulationModal';
import { SegmentEditorDialog } from '../environments/components/SegmentEditorDialog';
import { useCreateSegment } from '@/api/queries';

const ruleSchema = z.object({
    groupId: z.number(),
    attribute: z.string().min(1, 'Attribute is required'),
    operator: z.string().min(1, 'Operator is required'),
    value: z.string().min(1, 'Value is required'),
});

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
    const { data: segments } = useSegments(projectId, envId);

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

    const { fields, append, remove } = useFieldArray({
        control: form.control,
        name: 'rules',
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
                            🧪 Simulate Traffic
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
                                <div className="flex items-center justify-between">
                                    <div>
                                        <h3 className="text-lg font-medium flex items-center gap-2">
                                            <Settings2 className="h-5 w-5" /> Targeting Rules
                                        </h3>
                                        <p className="text-sm text-muted-foreground">Serve flag only if conditions match.</p>
                                    </div>
                                    <Button type="button" variant="outline" size="sm" onClick={addConditionOR} disabled={flag.isExperimentActive || !canEditEnv}>
                                        <Plus className="mr-2 h-4 w-4" /> Add Condition (OR)
                                    </Button>
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
                                                            <div className="flex items-start gap-3">
                                                                {form.watch(`rules.${field.index}.operator`) === 'InSegment' ? (
                                                                    <div className="flex-1 flex items-center h-9 px-3 border border-border/40 rounded-md bg-muted/30 text-sm text-muted-foreground shadow-sm">
                                                                        User Context
                                                                    </div>
                                                                ) : (
                                                                    <div className="flex-1 space-y-2">
                                                                        <Input
                                                                            placeholder="Attribute (e.g. Email)"
                                                                            disabled={flag.isExperimentActive || !canEditEnv}
                                                                            {...form.register(`rules.${field.index}.attribute`)}
                                                                            onKeyDown={(e) => {
                                                                                if (e.key === 'Enter') {
                                                                                    e.preventDefault();
                                                                                    form.handleSubmit(onSubmit)();
                                                                                }
                                                                            }}
                                                                        />
                                                                    </div>
                                                                )}
                                                                <div className="w-[180px]">
                                                                    <Select
                                                                        value={form.watch(`rules.${field.index}.operator`)}
                                                                        onValueChange={(val) => {
                                                                            const prev = form.watch(`rules.${field.index}.operator`);
                                                                            form.setValue(`rules.${field.index}.operator`, val);
                                                                            if (val === 'InSegment') {
                                                                                form.setValue(`rules.${field.index}.attribute`, 'segment');
                                                                                form.setValue(`rules.${field.index}.value`, segments?.[0]?.id || '');
                                                                            } else if (prev === 'InSegment') {
                                                                                form.setValue(`rules.${field.index}.attribute`, '');
                                                                                form.setValue(`rules.${field.index}.value`, '');
                                                                            }
                                                                        }}
                                                                        disabled={flag.isExperimentActive || !canEditEnv || isLoadingOperators}
                                                                    >
                                                                        <SelectTrigger>
                                                                            <SelectValue placeholder="Operator" />
                                                                        </SelectTrigger>
                                                                        <SelectContent>
                                                                            {operators.map(op => (
                                                                                <SelectItem key={op} value={op}>
                                                                                    {op}
                                                                                </SelectItem>
                                                                            ))}
                                                                        </SelectContent>
                                                                    </Select>
                                                                </div>
                                                                <div className="flex-1">
                                                                    {form.watch(`rules.${field.index}.operator`) === 'InSegment' ? (
                                                                        <Select
                                                                            value={form.watch(`rules.${field.index}.value`)}
                                                                            onValueChange={(val) => form.setValue(`rules.${field.index}.value`, val)}
                                                                            disabled={flag.isExperimentActive || !canEditEnv}
                                                                        >
                                                                            <SelectTrigger className="bg-muted/10 h-9">
                                                                                <SelectValue placeholder="Select segment..." />
                                                                            </SelectTrigger>
                                                                            <SelectContent>
                                                                                {segments?.map(s => (
                                                                                    <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>
                                                                                ))}
                                                                                {(!segments || segments.length === 0) && (
                                                                                    <SelectItem value="empty-state" disabled>No segments found</SelectItem>
                                                                                )}
                                                                                <div className="p-1 mt-1 border-t border-border/40">
                                                                                    <Button variant="ghost" className="w-full justify-start text-xs h-8 text-primary" onClick={(e) => {
                                                                                        e.stopPropagation();
                                                                                        if (canEditEnv) setIsCreateSegmentOpen(true);
                                                                                    }} disabled={!canEditEnv}>
                                                                                        <Plus className="h-3 w-3 mr-2" /> Create Segment
                                                                                    </Button>
                                                                                </div>
                                                                            </SelectContent>
                                                                        </Select>
                                                                    ) : (
                                                                        <Input
                                                                            placeholder="Value (e.g. @gmail.com)"
                                                                            disabled={flag.isExperimentActive || !canEditEnv}
                                                                            {...form.register(`rules.${field.index}.value`)}
                                                                            onKeyDown={(e) => {
                                                                                if (e.key === 'Enter') {
                                                                                    e.preventDefault();
                                                                                    form.handleSubmit(onSubmit)();
                                                                                }
                                                                            }}
                                                                        />
                                                                    )}
                                                                </div>
                                                                <Button
                                                                    type="button"
                                                                    variant="ghost"
                                                                    size="icon"
                                                                    onClick={() => remove(field.index)}
                                                                    className="h-9 w-9 text-muted-foreground hover:text-destructive hover:bg-destructive/10"
                                                                    disabled={flag.isExperimentActive || !canEditEnv}
                                                                >
                                                                    <Trash2 className="h-4 w-4" />
                                                                </Button>
                                                            </div>
                                                            {form.formState.errors.rules?.[field.index] && (
                                                                <p className="text-xs text-destructive mt-1">Please fill all fields.</p>
                                                            )}
                                                        </div>
                                                    ))}
                                                    <div className="pt-2">
                                                        <Button type="button" variant="outline" size="sm" onClick={() => addRuleAND(groupId)} disabled={flag.isExperimentActive || !canEditEnv}>
                                                            <Plus className="mr-1 h-3 w-3" /> Add Rule (AND)
                                                        </Button>
                                                    </div>
                                                </div>
                                            </div>
                                        );
                                    })}

                                    {fields.length === 0 && (
                                        <div className="text-center p-8 border border-dashed border-border/40 rounded-lg text-muted-foreground text-sm">
                                            No targeting rules defined. The flag will be served based on the rollout percentage.
                                        </div>
                                    )}
                                </div>
                            </div>

                            <SheetFooter className="mt-8 pt-4 border-t border-border/40">
                                <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
                                <Button type="submit" disabled={updateFlag.isPending || flag.isExperimentActive || !canEditEnv}>
                                    {updateFlag.isPending ? 'Saving...' : 'Save Changes'}
                                </Button>
                            </SheetFooter>
                        </form>
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