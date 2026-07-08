import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ArrowLeft, Box, Tag, Edit } from 'lucide-react';
import { useProjectFlags, useToggleFeatureFlag, useProjectDetails, useUpdateFlagMetadata, useFlagStats } from '@/api/queries';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Switch } from '@/components/ui/switch';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { toast } from 'sonner';
import { ProjectRole } from '@/api/types';
import { FeatureFlagEditor } from './FeatureFlagEditor';

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
import { handleApiError } from '@/api/errorUtils';

const flagMetaSchema = z.object({
    name: z.string().optional(),
    description: z.string().optional(),
    tags: z.string().optional()
});
type FlagMetaValues = z.infer<typeof flagMetaSchema>;

const formatNumber = (num: number) => {
    if (num === 0) return '0';
    if (num >= 1000000) return (num / 1000000).toFixed(1).replace(/\.0$/, '') + 'm';
    if (num >= 1000) return (num / 1000).toFixed(1).replace(/\.0$/, '') + 'k';
    return num.toLocaleString();
};

const calculatePercent = (value: number, total: number) => {
    if (total === 0) return 0;
    return (value / total) * 100;
};

export function ProjectFlagDetailsPage() {
    const { projectId, flagKey } = useParams<{ projectId: string; flagKey: string }>();
    const navigate = useNavigate();

    const { data: project, isLoading: isProjectLoading } = useProjectDetails(projectId!);
    const { data: flags, isLoading: isFlagsLoading } = useProjectFlags(projectId!);
    const toggleFlag = useToggleFeatureFlag(projectId!);
    const updateMetadata = useUpdateFlagMetadata(projectId!, flagKey!);

    const { data: stats } = useFlagStats(projectId!, flagKey!);

    const flag = flags?.find(f => f.key === flagKey);

    const [searchParams] = window.location.search ? [new URLSearchParams(window.location.search)] : [new URLSearchParams()];
    const initialEnvId = searchParams.get('envId');
    const [editingEnvId, setEditingEnvId] = useState<string | null>(initialEnvId);

    const [isMetaOpen, setIsMetaOpen] = useState(false);

    const metaForm = useForm<FlagMetaValues>({
        resolver: zodResolver(flagMetaSchema),
        defaultValues: { name: '', description: '', tags: '' }
    });

    const handleToggle = async (envId: string, targetValue: boolean) => {
        try {
            await toggleFlag.mutateAsync({ envId, flagKey: flagKey!, isEnabled: targetValue });
            toast.success(`Flag ${targetValue ? 'enabled' : 'disabled'} for environment`);
        } catch {
            toast.error('Failed to toggle flag');
        }
    };

    const handleOpenMeta = () => {
        if (flag) {
            metaForm.reset({
                name: flag.name || '',
                description: flag.description || '',
                tags: flag.tags?.join(', ') || ''
            });
            setIsMetaOpen(true);
        }
    };

    const handleSaveMeta = async (values: FlagMetaValues) => {
        try {
            const parsedTags = values.tags
                ? values.tags.split(',').map(t => t.trim()).filter(Boolean)
                : [];

            await updateMetadata.mutateAsync({
                name: values.name || '',
                description: values.description || '',
                tags: parsedTags
            });
            setIsMetaOpen(false);
            toast.success('Metadata updated successfully');
        } catch (error: any) {
            handleApiError(error, metaForm.setError, 'Failed to update metadata');
        }
    };

    if (isProjectLoading || isFlagsLoading) return <div className="p-8 text-center text-muted-foreground">Loading...</div>;
    if (!flag) return <div className="p-8 text-center text-muted-foreground">Flag not found</div>;

    const canEditMeta = project?.userRole === ProjectRole.Owner || project?.userRole === ProjectRole.Admin || project?.userRole === ProjectRole.Editor;

    return (
        <div className="space-y-6">
            <div className="flex items-center gap-2">
                <Button variant="ghost" size="sm" className="-ml-3 text-muted-foreground cursor-pointer" onClick={() => navigate(`/projects/${projectId}/flags`)}>
                    <ArrowLeft className="mr-2 h-4 w-4" />
                    Back to Flags
                </Button>
            </div>

            <Card className="border-border/40 bg-zinc-950/20">
                <CardContent className="p-4 sm:p-6 flex items-start justify-between gap-4">
                    <div className="space-y-3 flex-1 min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                            <h2 className="text-xl sm:text-2xl font-bold tracking-tight font-mono break-all">{flag.key}</h2>
                            {flag.isClientSideExposed && (
                                <Badge variant="secondary" className="bg-primary/10 text-primary text-[10px] shrink-0">Client Side</Badge>
                            )}
                        </div>
                        {flag.name && <h4 className="font-semibold text-sm">{flag.name}</h4>}
                        {flag.description && <p className="text-sm text-muted-foreground max-w-2xl">{flag.description}</p>}

                        <div className="flex items-center gap-1.5 flex-wrap pt-1">
                            <Tag className="h-3.5 w-3.5 text-muted-foreground" />
                            {flag.tags && flag.tags.length > 0 ? (
                                flag.tags.map(t => (
                                    <Badge key={t} variant="outline" className="text-[9px] font-sans font-medium uppercase bg-zinc-900/60 text-zinc-400 border-zinc-800">
                                        {t}
                                    </Badge>
                                ))
                            ) : (
                                <span className="text-xs text-muted-foreground font-mono">No tags configured</span>
                            )}
                        </div>
                    </div>
                    {canEditMeta && (
                        <Button variant="outline" size="sm" onClick={handleOpenMeta} className="cursor-pointer shrink-0">
                            <Edit className="mr-1.5 h-3.5 w-3.5" /> Edit Info
                        </Button>
                    )}
                </CardContent>
            </Card>

            <Card className="border-border/40 overflow-hidden">
                <Table>
                    <TableHeader className="sticky top-0 bg-background z-10 h-[41px]">
                        <TableRow className="hover:bg-transparent border-border/40 shadow-sm h-10">
                            <TableHead className="w-[200px]">Environment</TableHead>
                            <TableHead>Evaluations</TableHead>
                            <TableHead>Rules</TableHead>
                            <TableHead className="text-right">Status</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {project?.environments?.map(env => {
                            const state = flag.environments?.find(e => e.environmentId === env.id);
                            if (!state) return null;

                            const envStats = stats?.find(s => s.environmentId === env.id);
                            const trueCount = envStats?.trueCount || 0;
                            const falseCount = envStats?.falseCount || 0;

                            const canEditEnv = env.userRole === ProjectRole.Owner || env.userRole === ProjectRole.Admin || env.userRole === ProjectRole.Editor;

                            return (
                                <TableRow
                                    key={env.id}
                                    className="border-border/40 hover:bg-muted/30 cursor-pointer h-[53px] group"
                                    onClick={() => setEditingEnvId(env.id)}
                                >
                                    <TableCell className="font-medium text-sm">
                                        <div className="flex items-center gap-2">
                                            <Box className="h-4 w-4 text-muted-foreground" />
                                            {env.name}
                                        </div>
                                    </TableCell>
                                    <TableCell>
                                        <div className="flex flex-col gap-1.5 w-[140px] pr-4">
                                            <div className="flex items-center justify-between font-mono text-[13px] leading-none">
                                                <span className={trueCount > 0 ? "text-emerald-500/90 font-medium" : "text-muted-foreground/40"}>
                                                    <span className="text-[10px] text-muted-foreground/50 mr-1">T</span>
                                                    {formatNumber(trueCount)}
                                                </span>
                                                <span className={falseCount > 0 ? "text-rose-500/90 font-medium" : "text-muted-foreground/40"}>
                                                    {formatNumber(falseCount)}
                                                    <span className="text-[10px] text-muted-foreground/50 ml-1">F</span>
                                                </span>
                                            </div>
                                            <div className="h-1 w-full bg-secondary/40 rounded-full overflow-hidden flex">
                                                {(trueCount > 0 || falseCount > 0) ? (
                                                    <>
                                                        <div
                                                            className="h-full bg-emerald-500/60 transition-all"
                                                            style={{ width: `${calculatePercent(trueCount, trueCount + falseCount)}%` }}
                                                        />
                                                        <div
                                                            className="h-full bg-rose-500/60 transition-all"
                                                            style={{ width: `${calculatePercent(falseCount, trueCount + falseCount)}%` }}
                                                        />
                                                    </>
                                                ) : (
                                                    <div className="h-full w-full bg-muted-foreground/10" />
                                                )}
                                            </div>
                                        </div>
                                    </TableCell>
                                    <TableCell>
                                        <div className="flex gap-2">
                                            {state.rulesCount > 0 && (
                                                <Badge variant="secondary" className="bg-primary/10 text-primary min-w-[72px] justify-center">
                                                    {state.rulesCount} {state.rulesCount === 1 ? 'Rule' : 'Rules'}
                                                </Badge>
                                            )}
                                            {state.rolloutPercentage !== null && (
                                                <Badge variant="secondary" className="bg-accent text-accent-foreground">
                                                    {state.rolloutPercentage}% Rollout
                                                </Badge>
                                            )}
                                            {state.rulesCount === 0 && state.rolloutPercentage === null && (
                                                <span className="text-xs text-muted-foreground">Default</span>
                                            )}
                                        </div>
                                    </TableCell>
                                    <TableCell className="text-right">
                                        <div className="flex items-center justify-end gap-3" onClick={(e) => e.stopPropagation()}>
                                            <span className={`text-sm font-medium ${state.isEnabled ? 'text-primary' : 'text-muted-foreground'}`}>
                                                {state.isEnabled ? 'ON' : 'OFF'}
                                            </span>
                                            <Switch
                                                checked={state.isEnabled}
                                                onCheckedChange={(checked) => handleToggle(env.id, checked)}
                                                disabled={toggleFlag.isPending || !canEditEnv}
                                            />
                                        </div>
                                    </TableCell>
                                </TableRow>
                            );
                        })}
                    </TableBody>
                </Table>
            </Card>

            <Dialog open={isMetaOpen} onOpenChange={setIsMetaOpen}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle>Edit Flag Metadata</DialogTitle>
                        <DialogDescription>Update global settings for this feature flag.</DialogDescription>
                    </DialogHeader>
                    <Form {...metaForm}>
                        <form onSubmit={metaForm.handleSubmit(handleSaveMeta)}>
                            <div className="space-y-4 py-4">
                                <FormField
                                    control={metaForm.control}
                                    name="name"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Friendly Name</FormLabel>
                                            <FormControl>
                                                <Input placeholder="e.g. New Checkout System" {...field} />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                                <FormField
                                    control={metaForm.control}
                                    name="description"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Description</FormLabel>
                                            <FormControl>
                                                <Input placeholder="Describe what this flag controls..." {...field} />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                                <FormField
                                    control={metaForm.control}
                                    name="tags"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel className="flex items-center gap-1">
                                                <Tag className="h-3.5 w-3.5" /> Tags <span className="text-[10px] text-muted-foreground font-normal">(Global, comma-separated)</span>
                                            </FormLabel>
                                            <FormControl>
                                                <Input placeholder="e.g. billing, staging-only, stale" {...field} />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                            </div>
                            <DialogFooter>
                                <Button type="button" variant="outline" onClick={() => setIsMetaOpen(false)}>Cancel</Button>
                                <Button type="submit" disabled={updateMetadata.isPending}>Save</Button>
                            </DialogFooter>
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>

            {editingEnvId && (
                <EnvironmentFlagEditorWrapper
                    projectId={projectId!}
                    envId={editingEnvId}
                    flagKey={flag.key}
                    open={true}
                    onOpenChange={(open: boolean) => !open && setEditingEnvId(null)}
                />
            )}
        </div>
    );
}

import { useFeatureFlag } from '@/api/queries';

function EnvironmentFlagEditorWrapper({ projectId, envId, flagKey, open, onOpenChange }: any) {
    const { data: flag } = useFeatureFlag(projectId, envId, flagKey);
    const { data: project } = useProjectDetails(projectId);
    
    const env = project?.environments?.find(e => e.id === envId);
    const canEditEnv = env ? (env.userRole === ProjectRole.Owner || env.userRole === ProjectRole.Admin || env.userRole === ProjectRole.Editor) : false;

    return (
        <FeatureFlagEditor
            flag={flag || null}
            projectId={projectId}
            envId={envId}
            open={open}
            onOpenChange={onOpenChange}
            canEditEnv={canEditEnv}
        />
    );
}