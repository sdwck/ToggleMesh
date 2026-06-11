import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, ToggleRight } from 'lucide-react';
import { useProjectFlags, useCreateFeatureFlag, useToggleFeatureFlag, useUpdateFlagPrivacy } from '@/api/queries';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { toast } from 'sonner';
import type { ProjectDetails } from '@/api/types';

import { ProjectRole } from '@/api/types';
import { Input } from '@/components/ui/input';

interface ProjectFlagsTabProps {
  project: ProjectDetails;
}

export function ProjectFlagsTab({ project }: ProjectFlagsTabProps) {
  const navigate = useNavigate();
  const { data: flags, isLoading: isLoadingFlags } = useProjectFlags(project.id);
  const createFlag = useCreateFeatureFlag(project.id);
  const toggleFlag = useToggleFeatureFlag(project.id);
  const updatePrivacy = useUpdateFlagPrivacy(project.id);

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [newFlagKey, setNewFlagKey] = useState('');
  const [isCreating, setIsCreating] = useState(false);

  const canEditFlags = project.userRole === ProjectRole.Owner || project.userRole === ProjectRole.Admin || project.userRole === ProjectRole.Editor;

  const handleCreateFlag = async () => {
    if (!newFlagKey.trim()) return;

    setIsCreating(true);

    try {
      await createFlag.mutateAsync(newFlagKey.trim());
      toast.success(`Successfully created feature flag`);
      setNewFlagKey('');
      setIsCreateOpen(false);
    } catch (e) {
      console.error(`Failed to create flag`, e);
      toast.error('Failed to create flag. It might already exist.');
    } finally {
      setIsCreating(false);
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

  const handleTogglePrivacy = async (flagKey: string, isExposed: boolean) => {
    try {
      await updatePrivacy.mutateAsync({ flagKey, isClientSideExposed: isExposed });
      toast.success(`Flag is now ${isExposed ? 'exposed to client SDKs' : 'server-side only'}`);
    } catch {
      toast.error('Failed to update flag privacy');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-end">
        {canEditFlags && (
          <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
            <DialogTrigger asChild>
              <Button>
                <Plus className="mr-2 h-4 w-4" />
                New Flags
              </Button>
            </DialogTrigger>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Create Feature Flag</DialogTitle>
                <DialogDescription>
                  Enter a unique key for the new feature flag.
                </DialogDescription>
              </DialogHeader>
              <div className="py-4">
                <Input
                  placeholder="e.g. new-billing-ui"
                  value={newFlagKey}
                  onChange={(e) => setNewFlagKey(e.target.value)}
                  autoFocus
                  className="font-mono"
                />
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setIsCreateOpen(false)}>Cancel</Button>
                <Button onClick={handleCreateFlag} disabled={isCreating || !newFlagKey.trim()}>
                  {isCreating ? 'Creating...' : 'Create Flag'}
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
              <TableHead className="w-[250px]">Flag Key</TableHead>
              <TableHead className="w-[120px] text-center">Client Side</TableHead>
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
                  <TableCell><Skeleton className="h-4 w-[60px] mx-auto" /></TableCell>
                  {project.environments?.map(env => (
                    <TableCell key={env.id}><Skeleton className="h-6 w-12 mx-auto" /></TableCell>
                  ))}
                  <TableCell className="text-right"><Skeleton className="h-8 w-16 ml-auto" /></TableCell>
                </TableRow>
              ))
            ) : flags?.length === 0 ? (
              <TableRow className="border-border/40 h-[53px]">
                <TableCell colSpan={(project.environments?.length || 0) + 3} className="h-24 text-center text-muted-foreground">
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

                  <TableCell className="text-center py-2" onClick={(e) => e.stopPropagation()}>
                    <div className="flex flex-col items-center justify-center gap-1">
                      <Switch
                        checked={flag.isClientSideExposed}
                        onCheckedChange={(checked) => handleTogglePrivacy(flag.key, checked)}
                        disabled={updatePrivacy.isPending || !canEditFlags}
                      />
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