import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ArrowLeft, Box } from 'lucide-react';
import { useProjectFlags, useToggleFeatureFlag } from '@/api/queries';
import { useProjectDetails } from '@/api/queries';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Switch } from '@/components/ui/switch';
import { Badge } from '@/components/ui/badge';
import { toast } from 'sonner';
import { FeatureFlagEditor } from './FeatureFlagEditor';

import { ProjectRole } from '@/api/types';

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
  
  const { data: project } = useProjectDetails(projectId!);
  const { data: flags, isLoading } = useProjectFlags(projectId!);
  const toggleFlag = useToggleFeatureFlag(projectId!);

  const flag = flags?.find(f => f.key === flagKey);

  const [editingEnvId, setEditingEnvId] = useState<string | null>(null);

  const handleToggle = async (envId: string, targetValue: boolean) => {
    try {
      await toggleFlag.mutateAsync({ envId, flagKey: flagKey!, isEnabled: targetValue });
      toast.success(`Flag ${targetValue ? 'enabled' : 'disabled'} for environment`);
    } catch {
      toast.error('Failed to toggle flag');
    }
  };

  if (isLoading) return <div>Loading...</div>;
  if (!flag) return <div>Flag not found</div>;

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" className="-ml-3 text-muted-foreground" onClick={() => navigate(`/projects/${projectId}`)}>
        <ArrowLeft className="mr-2 h-4 w-4" />
        Back to Project
      </Button>

      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight font-mono">{flag.key}</h2>
          <p className="text-muted-foreground">Manage targeting rules and overrides across environments.</p>
        </div>
      </div>

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
            {project?.environments.map(env => {
              const state = flag.environments.find(e => e.environmentId === env.id);
              if (!state) return null;

              const canEditEnv = env.userRole === ProjectRole.Owner || env.userRole === ProjectRole.Admin || env.userRole === ProjectRole.Editor;

              return (
                <TableRow 
                  key={env.id} 
                  className="border-border/40 hover:bg-muted/30 cursor-pointer h-[53px]"
                  onClick={() => {
                    if (canEditEnv) setEditingEnvId(env.id);
                    else toast('You only have Viewer access to this environment.');
                  }}
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
                        <span className={state.trueCount > 0 ? "text-emerald-500/90 font-medium" : "text-muted-foreground/40"}>
                          <span className="text-[10px] text-muted-foreground/50 mr-1">T</span>
                          {formatNumber(state.trueCount)}
                        </span>
                        <span className={state.falseCount > 0 ? "text-rose-500/90 font-medium" : "text-muted-foreground/40"}>
                          {formatNumber(state.falseCount)}
                          <span className="text-[10px] text-muted-foreground/50 ml-1">F</span>
                        </span>
                      </div>
                      <div className="h-1 w-full bg-secondary/40 rounded-full overflow-hidden flex">
                        {(state.trueCount > 0 || state.falseCount > 0) ? (
                          <>
                            <div 
                              className="h-full bg-emerald-500/60 transition-all" 
                              style={{ width: `${calculatePercent(state.trueCount, state.trueCount + state.falseCount)}%` }} 
                            />
                            <div 
                              className="h-full bg-rose-500/60 transition-all" 
                              style={{ width: `${calculatePercent(state.falseCount, state.trueCount + state.falseCount)}%` }} 
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

    return (
        <FeatureFlagEditor
            flag={flag || null}
            projectId={projectId}
            envId={envId}
            open={open}
            onOpenChange={onOpenChange}
        />
    );
}
