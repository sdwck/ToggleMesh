import { useState } from 'react';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ShieldCheck, UserX, AlertTriangle, CheckCircle2, Layers } from 'lucide-react';
import { usePurgeIdentity } from '@/api/queries';
import type { ProjectDetails } from '@/api/types';
import { toast } from 'sonner';
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
    AlertDialogTrigger,
} from '@/components/ui/alert-dialog';

interface Props {
    projectId: string;
    project?: ProjectDetails;
    canManageProject: boolean;
}

export function ProjectSettingsPrivacyTab({ projectId, project, canManageProject }: Props) {
    const [purgeIdentity, setPurgeIdentity] = useState('');
    const [purgeScopeEnv, setPurgeScopeEnv] = useState<string>('all');
    const [isConfirmOpen, setIsConfirmOpen] = useState(false);

    const purgeMutation = usePurgeIdentity();

    const handlePurge = () => {
        if (!purgeIdentity.trim()) return;

        purgeMutation.mutate(
            {
                projectId,
                identity: purgeIdentity.trim(),
                environmentId: purgeScopeEnv === 'all' ? undefined : purgeScopeEnv
            },
            {
                onSuccess: (data: any) => {
                    const count = data.totalPurged ?? (data.exposuresPurged + data.tracksPurged);
                    toast.success(`GDPR Purge Complete`, {
                        description: `Successfully removed ${count} tracking and evaluation records for "${purgeIdentity.trim()}".`
                    });
                    setPurgeIdentity('');
                    setIsConfirmOpen(false);
                },
                onError: (err: any) => {
                    toast.error('Purge Failed', {
                        description: err.response?.data?.message || err.message || 'An error occurred while executing data erasure.'
                    });
                }
            }
        );
    };

    return (
        <div className="space-y-6">
            <Card className="border-border/40 bg-zinc-950/60 backdrop-blur-sm overflow-hidden shadow-sm">
                <CardHeader className="pb-4 px-6 pt-5 border-b border-border/20 bg-zinc-900/30">
                    <div className="space-y-1">
                        <CardTitle className="text-base font-semibold text-zinc-100 flex items-center gap-2">
                            <ShieldCheck className="h-5 w-5 text-zinc-400" /> Data Privacy & GDPR Erasure
                        </CardTitle>
                        <CardDescription className="text-xs text-muted-foreground">
                            Right to Erasure (GDPR Art. 17)
                        </CardDescription>
                    </div>
                </CardHeader>
                <CardContent className="space-y-6 px-6 pt-6">
                    <div className="p-4 rounded-lg bg-amber-500/10 border border-amber-500/20 text-amber-200/90 text-xs space-y-2">
                        <div className="flex items-center gap-2 font-semibold text-amber-300">
                            <AlertTriangle className="h-4 w-4 shrink-0 text-amber-400" />
                            <span>Right to be Forgotten (GDPR Article 17)</span>
                        </div>
                        <p className="leading-relaxed">
                            Executing a purge request permanently deletes all historical evaluation logs (<code className="font-mono text-[11px] text-amber-300">evaluations</code>)
                            and conversion tracking records (<code className="font-mono text-[11px] text-amber-300">conversion_events</code>) associated with the specified user identity across selected environments.
                        </p>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 items-start">
                        <div className="space-y-2">
                            <label className="text-xs font-medium text-zinc-300 flex items-center gap-1.5 h-5">
                                <UserX className="w-3.5 h-3.5 text-zinc-400" /> User Identity Key
                            </label>
                            <Input
                                value={purgeIdentity}
                                onChange={(e) => setPurgeIdentity(e.target.value)}
                                placeholder="e.g. user_123 or user@company.com"
                                className="border-zinc-800 bg-zinc-950 text-xs text-zinc-200 h-9 font-mono focus-visible:ring-1 focus-visible:ring-white/30"
                            />
                            <p className="text-[11px] text-muted-foreground">
                                The unique identifier used during SDK evaluation calls (e.g. <code className="font-mono text-zinc-400">user_id</code> or email).
                            </p>
                        </div>

                        <div className="space-y-2">
                            <label className="text-xs font-medium text-zinc-300 flex items-center gap-1.5 h-5">
                                <Layers className="w-3.5 h-3.5 text-zinc-400" /> Target Environment
                            </label>
                            <Select value={purgeScopeEnv} onValueChange={setPurgeScopeEnv}>
                                <SelectTrigger className="border-zinc-800 bg-zinc-950 text-xs text-zinc-200 h-9">
                                    <SelectValue placeholder="All Environments" />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="all">All Environments (Project-wide)</SelectItem>
                                    {project?.environments?.map(env => (
                                        <SelectItem key={env.id} value={env.id}>{env.name}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                            <p className="text-[11px] text-muted-foreground">
                                Limit data purging to a specific environment or wipe project-wide.
                            </p>
                        </div>
                    </div>
                </CardContent>

                <CardFooter className="bg-zinc-900/30 border-t border-border/20 px-6 py-4 flex items-center justify-between">
                    <div className="flex items-center gap-2 text-xs text-zinc-400">
                        <CheckCircle2 className="w-4 h-4 text-zinc-500" />
                        <span>Purging is immediate and irreversible.</span>
                    </div>

                    {canManageProject && (
                        <AlertDialog open={isConfirmOpen} onOpenChange={setIsConfirmOpen}>
                            <AlertDialogTrigger asChild>
                                <Button
                                    type="button"
                                    disabled={purgeMutation.isPending || !purgeIdentity.trim()}
                                    className="px-4 py-2 text-xs font-semibold bg-amber-600/20 hover:bg-amber-600/30 text-amber-300 border border-amber-500/30 transition-colors"
                                >
                                    <UserX className="w-3.5 h-3.5 mr-1.5 text-amber-400" />
                                    Purge Identity Data
                                </Button>
                            </AlertDialogTrigger>
                            <AlertDialogContent className="bg-zinc-950 border-zinc-800 max-w-sm">
                                <AlertDialogHeader>
                                    <AlertDialogTitle className="text-base text-zinc-100 flex items-center gap-2">
                                        <AlertTriangle className="h-5 w-5 text-amber-400" /> Confirm Data Erasure
                                    </AlertDialogTitle>
                                    <AlertDialogDescription asChild>
                                        <div className="text-xs text-zinc-400 space-y-2 mt-2">
                                            <p>
                                                Are you sure you want to permanently erase all telemetry and tracking logs for user identity <code className="font-mono text-amber-300 bg-amber-950/60 px-1 py-0.5 rounded">{purgeIdentity.trim()}</code>?
                                            </p>
                                            <p className="text-zinc-500">
                                                Scope: {purgeScopeEnv === 'all' ? 'All Environments' : project?.environments?.find(e => e.id === purgeScopeEnv)?.name || purgeScopeEnv}
                                            </p>
                                        </div>
                                    </AlertDialogDescription>
                                </AlertDialogHeader>
                                <AlertDialogFooter className="mt-4">
                                    <AlertDialogCancel className="border-zinc-800 text-xs">Cancel</AlertDialogCancel>
                                    <AlertDialogAction
                                        onClick={handlePurge}
                                        disabled={purgeMutation.isPending}
                                        className="bg-amber-600 hover:bg-amber-500 text-white text-xs font-semibold"
                                    >
                                        {purgeMutation.isPending ? 'Purging...' : 'Confirm Purge'}
                                    </AlertDialogAction>
                                </AlertDialogFooter>
                            </AlertDialogContent>
                        </AlertDialog>
                    )}
                </CardFooter>
            </Card>
        </div>
    );
}
