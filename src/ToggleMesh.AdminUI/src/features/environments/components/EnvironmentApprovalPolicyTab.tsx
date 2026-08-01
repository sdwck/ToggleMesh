import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle, CardFooter } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { Shield, ShieldAlert, Users, Minus, Plus, Lock } from 'lucide-react';
import { useUpdateEnvironmentApprovalPolicy, useProjectDetails } from '@/api/queries';
import { ProjectRole } from '@/api/types';
import { toastApiError } from '@/api/errorUtils';
import { toast } from 'sonner';

export function EnvironmentApprovalPolicyTab({ projectId, environmentId }: { projectId: string; environmentId: string }) {
    const { data: project } = useProjectDetails(projectId);
    const environment = project?.environments?.find(e => e.id === environmentId);
    const updatePolicy = useUpdateEnvironmentApprovalPolicy(projectId, environmentId);

    const isOwner = project?.userRole === ProjectRole.Owner;

    const [requireApprovals, setRequireApprovals] = useState(false);
    const [requiredApprovalsCount, setRequiredApprovalsCount] = useState(1);
    const [requireForProtectedFlagsOnly, setRequireForProtectedFlagsOnly] = useState(true);

    useEffect(() => {
        if (environment) {
            setRequireApprovals(environment.requireApprovals ?? false);
            setRequiredApprovalsCount(environment.requiredApprovalsCount ?? 1);
            setRequireForProtectedFlagsOnly(environment.requireForProtectedFlagsOnly ?? true);
        }
    }, [environment]);

    const hasChanges = Boolean(
        environment && (
            requireApprovals !== (environment.requireApprovals ?? false) ||
            requiredApprovalsCount !== (environment.requiredApprovalsCount ?? 1) ||
            requireForProtectedFlagsOnly !== (environment.requireForProtectedFlagsOnly ?? true)
        )
    );

    const handleSave = async () => {
        try {
            await updatePolicy.mutateAsync({
                requireApprovals,
                requiredApprovalsCount,
                requireForProtectedFlagsOnly,
            });
            toast.success('Approval policy updated successfully');
        } catch (error: any) {
            toastApiError(error, 'Failed to update policy');
        }
    };

    const incrementCount = () => {
        if (requiredApprovalsCount < 10) setRequiredApprovalsCount(prev => prev + 1);
    };

    const decrementCount = () => {
        if (requiredApprovalsCount > 1) setRequiredApprovalsCount(prev => prev - 1);
    };

    if (!isOwner) {
        return (
            <Card className="border-border/40 bg-zinc-950/40 p-8 text-center space-y-3 shadow-md">
                <div className="mx-auto w-10 h-10 rounded-full bg-rose-500/10 border border-rose-500/20 flex items-center justify-center text-rose-400">
                    <Lock className="w-5 h-5" />
                </div>
                <h3 className="text-sm font-semibold text-zinc-200">Access Restricted</h3>
                <p className="text-xs text-zinc-400 max-w-sm mx-auto">
                    Approval policy settings and parameters are restricted to Project Owners only.
                </p>
            </Card>
        );
    }

    return (
        <Card className="border-zinc-800 bg-zinc-950 shadow-2xl relative overflow-hidden">
            <CardHeader className="pb-4 border-b border-zinc-800/80">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                    <div className="flex items-start gap-3">
                        <div className="p-2 rounded-lg bg-zinc-900 border border-zinc-700/60 text-zinc-100 shadow-sm">
                            <Shield className="h-5 w-5" />
                        </div>
                        <div>
                            <CardTitle className="text-base font-semibold text-white">Change Approval Policy</CardTitle>
                            <CardDescription className="text-xs text-zinc-400 mt-0.5">
                                Enforce PR-style reviews and approvals before feature flag changes take effect in this environment.
                            </CardDescription>
                        </div>
                    </div>
                </div>
            </CardHeader>

            <CardContent className="space-y-5 pt-6">
                <div className={`p-4 rounded-lg border transition-colors ${requireApprovals
                    ? 'bg-zinc-900/90 border-zinc-600'
                    : 'bg-zinc-900/30 border-zinc-800 hover:border-zinc-700'
                    }`}>
                    <div className="flex items-center justify-between gap-4">
                        <div className="space-y-0.5">
                            <label htmlFor="enable-approvals-switch" className="text-sm font-semibold text-white flex items-center gap-2 cursor-pointer">
                                Enable Required Approvals
                            </label>
                            <p className="text-xs text-zinc-400">
                                When active, flag updates cannot be published directly and must be submitted for review.
                            </p>
                        </div>
                        <Switch
                            id="enable-approvals-switch"
                            disabled={!isOwner || updatePolicy.isPending}
                            checked={requireApprovals}
                            onCheckedChange={setRequireApprovals}
                            className="data-[state=checked]:bg-white data-[state=checked]:[&>span]:bg-black"
                        />
                    </div>
                </div>

                {requireApprovals && (
                    <div className="space-y-4 pt-1 animate-in fade-in duration-150">
                        <div className="p-4 rounded-lg border border-zinc-800 bg-zinc-900/50">
                            <div className="flex items-center justify-between gap-4">
                                <div className="space-y-0.5">
                                    <div className="flex items-center gap-2">
                                        <Lock className="h-4 w-4 text-zinc-400" />
                                        <label htmlFor="protected-flags-only-switch" className="text-sm font-semibold text-white cursor-pointer">
                                            Require for Protected Flags Only
                                        </label>
                                    </div>
                                    <p className="text-xs text-zinc-400">
                                        {requireForProtectedFlagsOnly ? (
                                            <>Approvals will be required <span className="text-emerald-400 font-medium">only for flags marked as Protected</span>. Standard flags can update immediately.</>
                                        ) : (
                                            <>Approvals will be required <span className="text-white font-medium">for all feature flags</span> in this environment.</>
                                        )}
                                    </p>
                                </div>
                                <Switch
                                    id="protected-flags-only-switch"
                                    disabled={!isOwner || updatePolicy.isPending}
                                    checked={requireForProtectedFlagsOnly}
                                    onCheckedChange={setRequireForProtectedFlagsOnly}
                                    className="data-[state=checked]:bg-white data-[state=checked]:[&>span]:bg-black"
                                />
                            </div>
                        </div>

                        <div className="p-4 rounded-lg border border-zinc-800 bg-zinc-900/50">
                            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                                <div className="space-y-0.5">
                                    <label htmlFor="required-approvers-input" className="text-sm font-semibold text-white flex items-center gap-2">
                                        <Users className="h-4 w-4 text-zinc-400" /> Required Approvers Count
                                    </label>
                                    <p className="text-xs text-zinc-400">
                                        Minimum number of Project Admins/Owners (other than the author) required to approve a change.
                                    </p>
                                </div>

                                <div className="flex items-center gap-1 bg-black p-1 rounded-md border border-zinc-700 self-start sm:self-auto">
                                    <Button
                                        type="button"
                                        variant="ghost"
                                        size="icon"
                                        disabled={!isOwner || updatePolicy.isPending || requiredApprovalsCount <= 1}
                                        onClick={decrementCount}
                                        className="h-7 w-7 text-zinc-300 hover:text-white hover:bg-zinc-800 rounded disabled:opacity-20 cursor-pointer"
                                    >
                                        <Minus className="h-3.5 w-3.5" />
                                    </Button>

                                    <Input
                                        id="required-approvers-input"
                                        type="number"
                                        min={1}
                                        max={10}
                                        disabled={!isOwner || updatePolicy.isPending}
                                        value={requiredApprovalsCount}
                                        onChange={(e) => {
                                            const val = Number.parseInt(e.target.value);
                                            if (!Number.isNaN(val) && val >= 1 && val <= 10) {
                                                setRequiredApprovalsCount(val);
                                            } else if (e.target.value === '') {
                                                setRequiredApprovalsCount(1);
                                            }
                                        }}
                                        className="w-10 h-7 bg-transparent border-0 text-center font-mono text-sm font-bold text-white focus-visible:ring-0 focus-visible:ring-offset-0 px-0 [appearance:textfield] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none"
                                    />

                                    <Button
                                        type="button"
                                        variant="ghost"
                                        size="icon"
                                        disabled={!isOwner || updatePolicy.isPending || requiredApprovalsCount >= 10}
                                        onClick={incrementCount}
                                        className="h-7 w-7 text-zinc-300 hover:text-white hover:bg-zinc-800 rounded disabled:opacity-20 cursor-pointer"
                                    >
                                        <Plus className="h-3.5 w-3.5" />
                                    </Button>
                                </div>
                            </div>
                        </div>
                    </div>
                )}

                {!isOwner && (
                    <div className="flex items-center gap-2 text-xs text-zinc-300 bg-zinc-900 p-3 rounded-lg border border-zinc-700">
                        <ShieldAlert className="h-4 w-4 shrink-0 text-zinc-400" />
                        Only Project Owners have permission to modify environment approval policies.
                    </div>
                )}
            </CardContent>

            {isOwner && (
                <CardFooter className="border-t border-zinc-800/80 px-6 py-3 bg-zinc-950 flex items-center justify-between">
                    <span className="text-xs text-zinc-500 font-mono">
                        Four-eyes principle enforced
                    </span>
                    <Button
                        size="sm"
                        disabled={updatePolicy.isPending || !hasChanges}
                        onClick={handleSave}
                        className="text-xs font-semibold px-4 bg-white text-black hover:bg-zinc-200 border border-white cursor-pointer transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        {updatePolicy.isPending ? 'Saving...' : 'Save Policy'}
                    </Button>
                </CardFooter>
            )}
        </Card>
    );
}


