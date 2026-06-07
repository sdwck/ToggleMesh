import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, ToggleRight } from 'lucide-react';
import { useProjectFlags, useCreateFeatureFlag, useToggleFeatureFlag } from '@/api/queries';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { toast } from 'sonner';
import type { ProjectDetails } from '@/api/types';

import { ProjectRole } from '@/api/types';

interface ProjectFlagsTabProps {
  project: ProjectDetails;
}

export function ProjectFlagsTab({ project }: ProjectFlagsTabProps) {
  const navigate = useNavigate();
  const { data: flags, isLoading: isLoadingFlags } = useProjectFlags(project.id);
  const createFlag = useCreateFeatureFlag(project.id);
  const toggleFlag = useToggleFeatureFlag(project.id);

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [newFlagKey, setNewFlagKey] = useState('');

  const canCreateFlag = project.userRole === ProjectRole.Owner || project.userRole === ProjectRole.Admin || project.userRole === ProjectRole.Editor;

  const handleCreateFlag = async () => {
    if (!newFlagKey.trim()) return;
    try {
      await createFlag.mutateAsync(newFlagKey);
      toast.success('Feature flag created globally');
      setNewFlagKey('');
      setIsCreateOpen(false);
    } catch {
      toast.error('Failed to create flag');
    }
  };

  const handleToggle = async (envId: string, flagKey: string, targetValue: boolean) => {
    try {
      await toggleFlag.mutateAsync({ envId, flagKey, isEnabled: targetValue });
      toast.success(`Flag ${targetValue ? 'enabled' : 'disabled'} for environment`);
    } catch {
      toast.error('Failed to toggle flag');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-end">
        {canCreateFlag && (
          <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
            <DialogTrigger asChild>
              <Button>
                <Plus className="mr-2 h-4 w-4" />
                New Flag
              </Button>
            </DialogTrigger>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Create Feature Flag</DialogTitle>
                <DialogDescription>
                  Creates a flag for the entire project. You can then override it per environment.
                </DialogDescription>
              </DialogHeader>
              <div className="py-4">
                <Input
                  placeholder="e.g., new-billing-ui"
                  value={newFlagKey}
                  onChange={(e) => setNewFlagKey(e.target.value)}
                  autoFocus
                  className="font-mono"
                />
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setIsCreateOpen(false)}>Cancel</Button>
                <Button onClick={handleCreateFlag} disabled={createFlag.isPending || !newFlagKey.trim()}>
                  {createFlag.isPending ? 'Creating...' : 'Create'}
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        )}
      </div>

      <Card className="border-border/40 overflow-hidden">
        <Table wrapperClassName="max-h-[600px] overflow-auto">
          <TableHeader className="sticky top-0 bg-background z-10 h-[41px]">
            <TableRow className="hover:bg-transparent border-border/40 shadow-sm h-10">
              <TableHead className="w-[300px]">Flag Key</TableHead>
              {project.environments?.map(env => (
                <TableHead key={env.id} className="text-center">{env.name}</TableHead>
              ))}
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoadingFlags ? (
              Array.from({ length: 3 }).map((_, i) => (
                <TableRow key={i} className="border-border/40 h-[53px]">
                  <TableCell><Skeleton className="h-4 w-[200px]" /></TableCell>
                  {project.environments?.map(env => (
                    <TableCell key={env.id}><Skeleton className="h-6 w-12 mx-auto" /></TableCell>
                  ))}
                  <TableCell className="text-right"><Skeleton className="h-8 w-16 ml-auto" /></TableCell>
                </TableRow>
              ))
            ) : flags?.length === 0 ? (
              <TableRow className="border-border/40 h-[53px]">
                <TableCell colSpan={(project.environments?.length || 0) + 2} className="h-24 text-center text-muted-foreground">
                  No feature flags found in this project.
                </TableCell>
              </TableRow>
            ) : (
              flags?.map((flag) => (
                <TableRow
                  key={flag.id}
                  className="border-border/40 hover:bg-muted/30 cursor-pointer h-[53px]"
                  onClick={() => navigate(`/projects/${project.id}/flags/${flag.key}`)}
                >
                  <TableCell className="font-medium font-mono text-sm py-2">
                    <div className="flex items-center gap-2">
                      <ToggleRight className="h-4 w-4 text-muted-foreground" />
                      {flag.key}
                    </div>
                  </TableCell>

                  {project.environments?.map(env => {
                    const state = flag.environments.find(e => e.environmentId === env.id);
                    const isEnabled = state?.isEnabled || false;
                    const canEditEnv = env.userRole === ProjectRole.Owner || env.userRole === ProjectRole.Admin || env.userRole === ProjectRole.Editor;

                    return (
                      <TableCell key={env.id} className="text-center py-2" onClick={(e) => e.stopPropagation()}>
                        <div className="inline-flex items-center justify-center">
                          <Switch
                            checked={isEnabled}
                            onCheckedChange={(checked) => handleToggle(env.id, flag.key, checked)}
                            disabled={toggleFlag.isPending || !canEditEnv}
                          />
                        </div>
                      </TableCell>
                    );
                  })}

                  <TableCell className="text-right py-2">
                    <Button variant="ghost" size="sm" className="h-8">
                      Manage
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </Card>
    </div>
  );
}
