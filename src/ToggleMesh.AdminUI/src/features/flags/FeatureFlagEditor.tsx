import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Lock, AlertCircle, Clock, Sparkles, ShieldCheck } from 'lucide-react';
import type { FeatureFlag } from '@/api/types';
import { Form, FormField, FormItem, FormLabel, FormControl } from '@/components/ui/form';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription } from '@/components/ui/sheet';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Separator } from '@/components/ui/separator';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from 'sonner';
import { useUpdateFeatureFlag, useRuleOperators, usePendingChanges, useCreatePendingChange, useProjectDetails, useSegments, useUpdateFlagProtection } from '@/api/queries';
import { ExperimentResults } from '../experiments/components/ExperimentResults';
import { SimulationModal } from '../experiments/components/SimulationModal';
import { SegmentEditorDialog } from '../environments/components/SegmentEditorDialog';
import { useCreateSegment } from '@/api/queries';
import { toastApiError } from '@/api/errorUtils';
import { ruleSchema } from './validation';
import { RulesConfigList } from './components/RulesConfigList';
import { RolloutConfig } from './components/RolloutConfig';
import { IndividualTargetsConfig } from './components/IndividualTargetsConfig';
import { PendingChangesTab } from './components/PendingChangesTab';

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
    })).optional(),
    isEnabled: z.boolean().optional()
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
    const createPendingChange = useCreatePendingChange(projectId, flag?.key || '', envId);

    const { data: project } = useProjectDetails(projectId);
    const environment = project?.environments?.find(e => e.id === envId);

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const envAny = environment as any;
    const requiresApproval = envAny?.requireApprovals && (!envAny?.requireForProtectedFlagsOnly || flag?.isProtected);

    const { data: dynamicOperators, isLoading: isLoadingOperators } = useRuleOperators();
    const operators = ['InSegment', ...(dynamicOperators || []).filter(op => op !== 'InSegment')];

    const form = useForm<FormValues>({
        resolver: zodResolver(formSchema) as any,
        defaultValues: {
            fallthroughRollout: [],
            rules: [],
            type: 0,
            variations: [],
            individualTargets: [],
            isEnabled: false
        },
    });

    const [formLoadedForFlag, setFormLoadedForFlag] = useState<string | null>(null);
    const [simOpen, setSimOpen] = useState(false);

    const [isProposeModalOpen, setIsProposeModalOpen] = useState(false);
    const [proposePayload, setProposePayload] = useState<any>(null);
    const [proposeComment, setProposeComment] = useState('');
    const [proposeExecuteAt, setProposeExecuteAt] = useState('');
    const [isScheduleOpen, setIsScheduleOpen] = useState(false);
    const [isViewPendingChangesOpen, setIsViewPendingChangesOpen] = useState(false);

    const { data: pendingChanges } = usePendingChanges(projectId, flag?.key || '', envId);
    const { data: segments } = useSegments(projectId, envId);
    const updateProtection = useUpdateFlagProtection(projectId, flag?.key || '');
    const [isConfirmUnprotectOpen, setIsConfirmUnprotectOpen] = useState(false);


    const activePendingChanges = (pendingChanges || []).filter(c => {
        if (c.status === 'PendingReview' || c.status === 'Approved') return true;
        if (c.status === 'Scheduled') {
            return c.approvedByUserIds && c.approvedByUserIds.length > 0;
        }
        return false;
    });
    const [pendingSaveValues, setPendingSaveValues] = useState<FormValues | null>(null);
    const [isPendingWarningOpen, setIsPendingWarningOpen] = useState(false);

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
                individualTargets: flag.individualTargets ? Object.entries(flag.individualTargets).map(([k, v]) => ({ key: k, variationId: v })) : [],
                isEnabled: flag.isEnabled ?? false
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

    const executeSave = async (values: FormValues) => {
        if (!flag) return;

        const payload: any = {};

        if (values.isEnabled !== flag.isEnabled) {
            payload.isEnabled = values.isEnabled;
        }

        const currentFallthrough = (values.fallthroughRollout || [])
            .map(r => ({ variationId: r.variationId, weight: Math.round(r.weight * 100) }))
            .sort((a, b) => a.variationId.localeCompare(b.variationId));
            
        const initialFallthrough = (flag.fallthroughRollout || [])
            .map(r => ({ variationId: r.variationId, weight: r.weight }))
            .sort((a, b) => a.variationId.localeCompare(b.variationId));

        if (JSON.stringify(currentFallthrough) !== JSON.stringify(initialFallthrough)) {
            payload.fallthroughRollout = currentFallthrough;
        }

        const currentRules = (values.rules || []).map((r, idx) => ({
            priority: r.priority ?? idx,
            groupId: r.groupId || null,
            attribute: r.attribute || '',
            operator: r.operator || '',
            value: typeof r.value === 'string' ? r.value : JSON.stringify(r.value || ''),
            rollout: (r.rollout || [])
                .map(ro => ({ variationId: ro.variationId, weight: Math.round(ro.weight * 100) }))
                .sort((a, b) => a.variationId.localeCompare(b.variationId))
        }));

        const initialRules = (flag.rules || []).map((r, idx) => ({
            priority: r.priority ?? idx,
            groupId: r.groupId || null,
            attribute: r.attribute || '',
            operator: r.operator || '',
            value: typeof r.value === 'string' ? r.value : JSON.stringify(r.value || ''),
            rollout: (r.rollout || [])
                .map(ro => ({ variationId: ro.variationId, weight: ro.weight }))
                .sort((a, b) => a.variationId.localeCompare(b.variationId))
        }));

        if (JSON.stringify(currentRules) !== JSON.stringify(initialRules)) {
            payload.rules = currentRules;
        }

        const currentTargets = values.individualTargets?.reduce((acc, curr) => {
            if (curr.key?.trim()) acc[curr.key.trim()] = curr.variationId;
            return acc;
        }, {} as Record<string, string>) || {};
        const initialTargets = flag.individualTargets || {};
        if (JSON.stringify(currentTargets) !== JSON.stringify(initialTargets)) {
            payload.individualTargets = currentTargets;
        }

        if (Object.keys(payload).length === 0) {
            toast.info('No changes made');
            onOpenChange(false);
            return;
        }

        if (requiresApproval) {
            setProposePayload(payload);
            setProposeComment('');
            setIsProposeModalOpen(true);
            return;
        }

        if (proposeExecuteAt) {
            try {
                await createPendingChange.mutateAsync({
                    patchInstructionsJson: JSON.stringify(payload),
                    executeAt: new Date(proposeExecuteAt).toISOString()
                });
                toast.success('Change scheduled successfully');
                onOpenChange(false);
            } catch (error: any) {
                toastApiError(error, 'Failed to schedule change');
            }
            return;
        }

        try {
            await updateFlag.mutateAsync(payload);
            toast.success('Feature flag updated');
            onOpenChange(false);
        } catch (error: any) {
            toastApiError(error, 'Failed to update feature flag');
        }
    };

    const onSubmit = async (values: FormValues) => {
        if (activePendingChanges.length > 0) {
            setPendingSaveValues(values);
            setIsPendingWarningOpen(true);
            return;
        }
        await executeSave(values);
    };

    const [searchParams] = window.location.search ? [new URLSearchParams(window.location.search)] : [new URLSearchParams()];
    const defaultTab = searchParams.get('tab') || 'rules';

    const handleProposeSubmit = async () => {
        if (!proposePayload || Object.keys(proposePayload).length === 0) {
            toast.info('No changes to submit');
            setIsProposeModalOpen(false);
            return;
        }
        try {
            await createPendingChange.mutateAsync({
                patchInstructionsJson: JSON.stringify(proposePayload),
                comment: proposeComment,
                executeAt: proposeExecuteAt ? new Date(proposeExecuteAt).toISOString() : undefined
            });
            toast.success('Change request submitted for review');
            setIsProposeModalOpen(false);
            onOpenChange(false);
        } catch (error: any) {
            toastApiError(error, 'Failed to submit change request');
        }
    };

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
                        <SheetTitle className="font-mono flex items-center gap-2">
                            {flag.key}
                            {flag.isProtected && (
                                <span title="Flag is Protected"><ShieldCheck className="h-4 w-4 text-emerald-400" /></span>
                            )}
                        </SheetTitle>
                        <SheetDescription>Configure targeting rules and rollout strategy.</SheetDescription>
                    </div>
                    <div className="flex items-center gap-2">
                        {import.meta.env.DEV && (
                            <Button type="button" variant="outline" size="sm" className="gap-2 border-emerald-500/30 text-emerald-500 hover:bg-emerald-500/10 h-8 text-xs cursor-pointer" onClick={() => setSimOpen(true)}>
                                Simulate Traffic
                            </Button>
                        )}
                    </div>
                </SheetHeader>

                <Dialog open={isConfirmUnprotectOpen} onOpenChange={setIsConfirmUnprotectOpen}>
                    <DialogContent className="bg-zinc-950 border-zinc-800/60">
                        <DialogHeader>
                            <DialogTitle>Unprotect Flag</DialogTitle>
                            <DialogDescription>
                                Are you sure you want to remove protection from this flag? This will disable the requirement for change requests in affected environments.
                            </DialogDescription>
                        </DialogHeader>
                        <DialogFooter>
                            <Button variant="outline" onClick={() => setIsConfirmUnprotectOpen(false)}>Cancel</Button>
                            <Button variant="destructive" disabled={updateProtection.isPending} onClick={async () => {
                                try {
                                    await updateProtection.mutateAsync(false);
                                    toast.success('Protection removed');
                                    setIsConfirmUnprotectOpen(false);
                                } catch (err: any) {
                                    toast.error(err?.response?.data?.message || 'Failed to remove protection');
                                }
                            }}>
                                Confirm Unprotect
                            </Button>
                        </DialogFooter>
                    </DialogContent>
                </Dialog>

                <SimulationModal
                    open={simOpen}
                    onOpenChange={setSimOpen}
                    projectId={projectId}
                    envId={envId}
                    flag={flag}
                />

                <Dialog open={isPendingWarningOpen} onOpenChange={setIsPendingWarningOpen}>
                    <DialogContent className="bg-zinc-950 border-amber-500/30 sm:max-w-md">
                        <DialogHeader>
                            <DialogTitle className="flex items-center gap-2 text-amber-400">
                                <AlertCircle className="h-5 w-5 text-amber-400" />
                                Pending Changes Exist
                            </DialogTitle>
                            <DialogDescription className="text-zinc-300 pt-2" asChild>
                                <div>
                                    <div className="p-3 mb-2 rounded bg-amber-500/10 border border-amber-500/40 text-amber-300 text-sm flex flex-col gap-2">
                                        <div className="flex items-start gap-2">
                                            <AlertCircle className="w-4 h-4 mt-0.5 shrink-0" />
                                            <div className="flex flex-col gap-1">
                                                <span className="font-semibold text-xs uppercase tracking-wider text-amber-400">Active Change Requests Detected</span>
                                                <span className="text-xs">There are {activePendingChanges.length} active or scheduled change request(s) for this environment. Making direct edits now may overwrite or conflict with those pending changes.</span>
                                            </div>
                                        </div>
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            className="self-start text-amber-400 border-amber-500/40 hover:bg-amber-500/20 text-xs py-1 h-7"
                                            onClick={(e) => {
                                                e.preventDefault();
                                                setIsViewPendingChangesOpen(true);
                                            }}
                                        >
                                            View Pending Changes
                                        </Button>
                                    </div>
                                    <p className="text-sm">Are you sure you want to proceed?</p>
                                </div>
                            </DialogDescription>
                        </DialogHeader>
                        <DialogFooter className="flex flex-col sm:flex-row gap-2 mt-4">
                            <Button
                                variant="outline"
                                onClick={() => {
                                    setIsPendingWarningOpen(false);
                                    setPendingSaveValues(null);
                                }}
                            >
                                Cancel
                            </Button>
                            <Button
                                className="bg-amber-500 hover:bg-amber-600 text-black font-semibold cursor-pointer"
                                onClick={() => {
                                    setIsPendingWarningOpen(false);
                                    if (pendingSaveValues) {
                                        executeSave(pendingSaveValues);
                                        setPendingSaveValues(null);
                                    }
                                }}
                            >
                                Confirm & Save
                            </Button>
                        </DialogFooter>
                    </DialogContent>
                </Dialog>

                <Form {...form}>
                    <form onSubmit={form.handleSubmit(onSubmit as any)} className="pb-10">
                        <div className="mt-6">
                            <FormField
                                control={form.control}
                                name="isEnabled"
                                render={({ field }: { field: any }) => (
                                    <FormItem className="flex items-center justify-between space-y-0 bg-zinc-900/40 px-4 py-3 rounded-xl border border-zinc-800/60 shadow-inner">
                                        <div className="space-y-0.5">
                                            <FormLabel className="text-base font-semibold text-zinc-100">
                                                Flag Status
                                            </FormLabel>
                                            <p className="text-xs text-muted-foreground">
                                                Enable or disable this flag for the current environment.
                                            </p>
                                        </div>
                                        <div className="flex items-center gap-3">
                                            <span className={`text-sm font-bold ${field.value ? 'text-primary' : 'text-muted-foreground'}`}>
                                                {field.value ? 'ON' : 'OFF'}
                                            </span>
                                            <FormControl>
                                                <Switch
                                                    checked={field.value}
                                                    onCheckedChange={field.onChange}
                                                    disabled={!canEditEnv}
                                                />
                                            </FormControl>
                                        </div>
                                    </FormItem>
                                )}
                            />
                        </div>
                        <Tabs defaultValue={defaultTab} className="w-full mt-6">
                            <TabsList className="grid w-full bg-zinc-900/50 grid-cols-2">
                                <TabsTrigger value="rules">Targeting Rules</TabsTrigger>
                                <TabsTrigger value="experiments">A/B Testing</TabsTrigger>
                            </TabsList>

                            <TabsContent value="rules" className="mt-4 space-y-8">
                                {pendingChanges && pendingChanges.some(c => c.status === 'PendingReview' || c.status === 'Scheduled') && (
                                    <div className="mb-6 p-4 rounded-lg bg-blue-500/10 border border-blue-500/20 text-sm flex items-start gap-3">
                                        <AlertCircle className="h-5 w-5 text-blue-400 shrink-0 mt-0.5" />
                                        <div>
                                            <h4 className="font-medium text-blue-400 mb-1">Pending Changes Scheduled</h4>
                                            <p className="text-blue-300/80">There are pending or scheduled changes for this environment. Editing now may cause conflicts when those changes execute.</p>
                                        </div>
                                    </div>
                                )}

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
                                        segments={segments}
                                    />
                                </div>

                                <div className="mt-8 pt-4 border-t border-border/40 space-y-4">
                                    {(isScheduleOpen || proposeExecuteAt) && (
                                        <div className="flex flex-col space-y-3 bg-zinc-900/30 p-4 rounded-xl border border-zinc-800/60 shadow-inner animate-in fade-in slide-in-from-bottom-2 duration-200">
                                            <div className="flex items-center justify-between">
                                                <Label htmlFor="executeAt" className="text-xs font-semibold flex items-center gap-2 text-zinc-200">
                                                    <Clock className="h-3.5 w-3.5 text-zinc-400" />
                                                    Schedule Execution
                                                </Label>
                                                {proposeExecuteAt && (
                                                    <button
                                                        type="button"
                                                        onClick={() => {
                                                            const d = new Date(Date.now() + 3600 * 1000);
                                                            d.setMinutes(0, 0, 0);
                                                            setProposeExecuteAt(new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16));
                                                        }}
                                                        className="text-xs text-zinc-400 hover:text-white transition-colors cursor-pointer font-medium"
                                                    >
                                                        Reset Schedule
                                                    </button>
                                                )}
                                            </div>

                                            <div className="flex flex-col sm:flex-row gap-3">
                                                <Input
                                                    id="executeAt"
                                                    type="datetime-local"
                                                    value={proposeExecuteAt}
                                                    onChange={(e) => setProposeExecuteAt(e.target.value)}
                                                    disabled={!canEditEnv}
                                                    style={{ colorScheme: 'dark' }}
                                                    className="bg-zinc-950 border-zinc-800 focus:border-zinc-500 text-zinc-100 text-sm font-mono h-10 cursor-pointer transition-all shadow-sm flex-1 relative [&::-webkit-calendar-picker-indicator]:absolute [&::-webkit-calendar-picker-indicator]:right-3 [&::-webkit-calendar-picker-indicator]:cursor-pointer"
                                                />
                                                <div className="flex gap-2 shrink-0">
                                                    <Button
                                                        type="button"
                                                        variant="outline"
                                                        onClick={() => {
                                                            const baseTime = proposeExecuteAt ? new Date(proposeExecuteAt).getTime() : Date.now();
                                                            const d = new Date(baseTime + 3600 * 1000);
                                                            setProposeExecuteAt(new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16));
                                                        }}
                                                        disabled={!canEditEnv}
                                                        className="h-10 text-xs font-medium bg-zinc-950 border-zinc-800 hover:bg-zinc-900 cursor-pointer"
                                                    >
                                                        +1h
                                                    </Button>
                                                    <Button
                                                        type="button"
                                                        variant="outline"
                                                        onClick={() => {
                                                            const baseTime = proposeExecuteAt ? new Date(proposeExecuteAt).getTime() : Date.now();
                                                            const d = new Date(baseTime + 24 * 3600 * 1000);
                                                            setProposeExecuteAt(new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16));
                                                        }}
                                                        disabled={!canEditEnv}
                                                        className="h-10 text-xs font-medium bg-zinc-950 border-zinc-800 hover:bg-zinc-900 cursor-pointer"
                                                    >
                                                        +24h
                                                    </Button>
                                                </div>
                                            </div>

                                            {proposeExecuteAt ? (
                                                <div className="text-xs text-blue-400 bg-blue-500/10 px-3 py-2 rounded-lg border border-blue-500/20 flex items-center gap-2 animate-in fade-in zoom-in-95 duration-200">
                                                    <Sparkles className="h-4 w-4 shrink-0 text-blue-400" />
                                                    <span>Will automatically execute on <strong className="font-mono bg-blue-500/20 px-1 py-0.5 rounded">{new Date(proposeExecuteAt).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })}</strong></span>
                                                </div>
                                            ) : (
                                                <p className="text-xs text-zinc-500">
                                                    Select date/time for scheduled release.
                                                </p>
                                            )}
                                        </div>
                                    )}

                                    <div className="flex items-center justify-between pt-2">
                                        <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>

                                        <div className="flex items-center gap-3">
                                            <Button
                                                type="button"
                                                variant="outline"
                                                onClick={() => {
                                                    if (isScheduleOpen || proposeExecuteAt) {
                                                        setIsScheduleOpen(false);
                                                        setProposeExecuteAt('');
                                                    } else {
                                                        setIsScheduleOpen(true);
                                                        const d = new Date(Date.now() + 3600 * 1000);
                                                        d.setMinutes(0, 0, 0);
                                                        setProposeExecuteAt(new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16));
                                                    }
                                                }}
                                                className={`text-xs gap-1.5 cursor-pointer border-zinc-800 ${(isScheduleOpen || proposeExecuteAt) ? 'bg-blue-500/10 text-blue-400 border-blue-500/30 hover:bg-blue-500/20' : 'hover:bg-zinc-900 text-zinc-300'
                                                    }`}
                                            >
                                                <Clock className="h-3.5 w-3.5" />
                                                {proposeExecuteAt ? 'Scheduled' : 'Schedule'}
                                            </Button>

                                            <Button
                                                type="submit"
                                                disabled={updateFlag.isPending || createPendingChange.isPending || !canEditEnv}
                                                className={(requiresApproval || proposeExecuteAt) ? "bg-white text-black hover:bg-zinc-200 border border-white font-semibold cursor-pointer shadow-md" : ""}
                                            >
                                                {requiresApproval
                                                    ? 'Propose Change'
                                                    : proposeExecuteAt
                                                        ? (createPendingChange.isPending ? 'Scheduling...' : 'Schedule Change')
                                                        : (updateFlag.isPending ? 'Saving...' : 'Save Changes')}
                                            </Button>
                                        </div>
                                    </div>
                                </div>
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

            <Dialog open={isProposeModalOpen} onOpenChange={setIsProposeModalOpen}>
                <DialogContent className="sm:max-w-[425px] bg-zinc-950 border-border/40">
                    <DialogHeader>
                        <DialogTitle>Propose Flag Changes</DialogTitle>
                        <DialogDescription>
                            This environment requires approvals for these changes. Describe your changes and optionally schedule them for later.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="grid gap-4 py-4">
                        <div className="flex flex-col space-y-3 bg-zinc-900/30 p-4 rounded-xl border border-zinc-800/60 shadow-inner">
                            <Label htmlFor="comment" className="text-sm font-semibold flex items-center gap-2 text-zinc-200">
                                <span>Comment</span>
                                <span className="text-[10px] uppercase font-bold tracking-wider text-zinc-500 bg-zinc-800/50 px-1.5 py-0.5 rounded">Optional</span>
                            </Label>
                            <Input
                                id="comment"
                                placeholder="Why are you making this change?"
                                value={proposeComment}
                                onChange={(e) => setProposeComment(e.target.value)}
                                className="bg-zinc-950 border-zinc-800 focus:border-zinc-500 text-zinc-100 text-sm h-10 shadow-sm"
                            />
                            <p className="text-xs text-zinc-500">
                                Provide context for the reviewers.
                            </p>
                        </div>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setIsProposeModalOpen(false)}>Cancel</Button>
                        <Button onClick={handleProposeSubmit} disabled={createPendingChange.isPending} className="bg-white text-black hover:bg-zinc-200 border border-white font-semibold cursor-pointer">
                            {createPendingChange.isPending ? 'Submitting...' : 'Submit Request'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
            <Dialog open={isViewPendingChangesOpen} onOpenChange={setIsViewPendingChangesOpen}>
                <DialogContent className="w-full sm:max-w-4xl bg-zinc-950 p-0 border-zinc-800 max-h-[85vh] overflow-hidden flex flex-col">
                    <DialogHeader className="p-6 pb-4 shrink-0 border-b border-zinc-800 bg-zinc-900/50 flex flex-row items-center justify-between space-y-0">
                        <div className="space-y-1">
                            <DialogTitle>Pending Changes for {flag.key}</DialogTitle>
                            <DialogDescription>
                                All active and historical change requests for this environment.
                            </DialogDescription>
                        </div>
                        <div id="pending-changes-header-actions" className="flex items-center justify-end flex-1 pr-8" />
                    </DialogHeader>
                    <div className="flex-1 overflow-y-auto p-6 bg-zinc-950/50">
                        <PendingChangesTab
                            projectId={projectId}
                            flagKey={flag.key}
                            environmentId={envId}
                        />
                    </div>
                </DialogContent>
            </Dialog>
        </Sheet>
    );
}