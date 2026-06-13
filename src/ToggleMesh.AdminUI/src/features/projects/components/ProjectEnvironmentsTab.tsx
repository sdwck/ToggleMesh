import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Box, ArrowRightLeft, FileClock, Settings } from 'lucide-react';
import { useCreateEnvironment, useCloneEnvironment, useAuditLogs } from '@/api/queries';      
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PaginationControls } from '@/components/ui/PaginationControls';
import { ProjectRole, type AuditLog, type Environment, type ProjectDetails } from '@/api/types';
import { toast } from 'sonner';
import { Skeleton } from '@/components/ui/skeleton';

const formatDate = (dateString: string) => {
  const date = new Date(dateString);
  const pad = (num: number) => String(num).padStart(2, '0');
  
  const year = date.getFullYear();
  const month = pad(date.getMonth() + 1);
  const day = pad(date.getDate());
  const hours = pad(date.getHours());
  const minutes = pad(date.getMinutes());
  const seconds = pad(date.getSeconds());
  
  return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
};

function EnvironmentAuditLogs({ envId }: { envId: string }) {
  const [page, setPage] = useState(1);
  const pageSize = 6;

  const { data, isLoading } = useAuditLogs(envId, page, pageSize);
  const [selectedLog, setSelectedLog] = useState<AuditLog | null>(null);

  return (
    <div className="space-y-4">
      <div className="rounded-md border border-border/40 overflow-hidden">
        <Table wrapperClassName="max-h-[400px] overflow-auto">
          <TableHeader className="sticky top-0 bg-background z-10">
            <TableRow className="hover:bg-transparent">
              <TableHead className="w-[180px]">Timestamp</TableHead>
              <TableHead>Action</TableHead>
              <TableHead>Entity</TableHead>
              <TableHead>Performed By</TableHead>
              <TableHead className="text-right">Details</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableRow>
                <TableCell colSpan={5} className="h-24">
                  <div className="flex flex-col gap-2">
                    <Skeleton className="h-4 w-full" />
                    <Skeleton className="h-4 w-full" />
                    <Skeleton className="h-4 w-full" />
                  </div>
                </TableCell>
              </TableRow>
            ) : data?.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} className="h-24 text-center text-muted-foreground">
                  No audit logs for this environment.
                </TableCell>
              </TableRow>
            ) : (
              data?.items.map((log) => (
                <TableRow key={log.id} className="hover:bg-muted/30 text-sm">
                  <TableCell className="text-muted-foreground whitespace-nowrap font-mono text-xs">
                    {formatDate(log.timestamp)}
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline" className="text-[10px] font-mono uppercase">
                      {log.action}
                    </Badge>
                  </TableCell>
                  <TableCell className="font-mono text-xs text-primary/80">
                    {log.entityName} ({log.entityFriendlyName || log.entityId})
                  </TableCell>
                  <TableCell className="text-muted-foreground whitespace-nowrap font-mono text-xs">
                    {log.performedBy}
                  </TableCell>
                  <TableCell className="text-right">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setSelectedLog(log)}
                      disabled={!log.oldValues && !log.newValues}
                      className="h-8"
                    >
                      View
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {data && data.totalPages > 1 && (
        <div className="pt-2 border-t border-border/40">
          <PaginationControls
            currentPage={page}
            totalPages={data.totalPages}
            onPageChange={setPage}
            hasNextPage={data.hasNextPage}
            hasPreviousPage={data.hasPreviousPage}
          />
        </div>
      )}

      {selectedLog && (
        <Dialog open={!!selectedLog} onOpenChange={(open) => !open && setSelectedLog(null)}>
          <DialogContent className="max-w-3xl max-h-[80vh] overflow-y-auto z-[110]">
            <DialogHeader>
              <DialogTitle>Audit Details</DialogTitle>
              <DialogDescription>
                {selectedLog.action} on {selectedLog.entityName} at {new Date(selectedLog.timestamp).toLocaleString()} 
              </DialogDescription>
            </DialogHeader>
            <div className="grid grid-cols-2 gap-4 mt-4">
              <div className="space-y-2">
                <h4 className="text-sm font-medium text-muted-foreground">Previous State</h4>
                <div className="bg-zinc-950 p-4 rounded-md font-mono text-xs overflow-x-auto border border-border/40 text-emerald-500/80">
                  {selectedLog.oldValues && selectedLog.oldValues !== "{}" ? (
                    <pre>{JSON.stringify(JSON.parse(selectedLog.oldValues), null, 2)}</pre>
                  ) : <span className="text-zinc-600">None</span>}
                </div>
              </div>
              <div className="space-y-2">
                <h4 className="text-sm font-medium text-muted-foreground">New State</h4>
                <div className="bg-zinc-950 p-4 rounded-md font-mono text-xs overflow-x-auto border border-border/40 text-emerald-400">
                  {selectedLog.newValues && selectedLog.newValues !== "{}" ? (
                    <pre>{JSON.stringify(JSON.parse(selectedLog.newValues), null, 2)}</pre>
                  ) : <span className="text-zinc-600">None</span>}
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button onClick={() => setSelectedLog(null)}>Close</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </div>
  );
}

export function ProjectEnvironmentsTab({ project }: { project: ProjectDetails }) {
  const navigate = useNavigate();
  const createEnvironment = useCreateEnvironment(project.id);
  const cloneEnvironment = useCloneEnvironment(project.id);

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [newEnvName, setNewEnvName] = useState('');

  const [envToSync, setEnvToSync] = useState<string | null>(null);
  const [syncSourceEnv, setSyncSourceEnv] = useState<string>('');

  const [auditEnvId, setAuditEnvId] = useState<string | null>(null);

  const canManageProject = project.userRole === ProjectRole.Owner || project.userRole === ProjectRole.Admin;

  const handleCreateEnv = async () => {
    if (!newEnvName.trim()) return;
    try {
      await createEnvironment.mutateAsync(newEnvName);
      toast.success('Environment created');
      setNewEnvName('');
      setIsCreateOpen(false);
    } catch {
      toast.error('Failed to create environment');
    }
  };

  const handleSyncEnvironment = async () => {
    if (!envToSync || !syncSourceEnv) return;
    try {
      await cloneEnvironment.mutateAsync({ sourceEnvId: syncSourceEnv, targetEnvId: envToSync });
      toast.success('Environment rules synchronized successfully');
      setEnvToSync(null);
    } catch {
      toast.error('Failed to sync rules');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">Environments</h2>
          <p className="text-muted-foreground">Manage environments for this project.</p>
        </div>
        {canManageProject && (
          <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
            <DialogTrigger asChild>
              <Button>
                <Plus className="mr-2 h-4 w-4" />
                New Environment
              </Button>
            </DialogTrigger>
            <DialogContent className="border-border/40 bg-zinc-950">
              <DialogHeader>
                <DialogTitle>Create Environment</DialogTitle>
                <DialogDescription>
                  Environments have separate feature flags and API keys.
                </DialogDescription>
              </DialogHeader>
              <div className="py-4">
                <Input
                  placeholder="e.g., Production, Staging"
                  value={newEnvName}
                  onChange={(e) => setNewEnvName(e.target.value)}
                  autoFocus
                />
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setIsCreateOpen(false)}>Cancel</Button>
                <Button onClick={handleCreateEnv} disabled={createEnvironment.isPending || !newEnvName.trim()}>
                  {createEnvironment.isPending ? 'Creating...' : 'Create'}
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        )}
      </div>

      <div className="grid gap-6">
        {project.environments.map((env: Environment) => {
          const canManageEnv = env.userRole === ProjectRole.Owner || env.userRole === ProjectRole.Admin;

          return (
          <Card key={env.id} className="border-border/40 hover:border-primary/20 transition-colors cursor-pointer" onClick={() => navigate(`/projects/${project.id}/environments/${env.id}`)}>
            <CardHeader className="flex flex-row items-center justify-between py-4 space-y-0">
              <div className="space-y-1">
                <CardTitle className="text-lg flex items-center gap-2">
                  <Box className="h-5 w-5 text-muted-foreground" />
                  {env.name}
                </CardTitle>
              </div>
              <div className="flex gap-2" onClick={(e) => e.stopPropagation()}>
                  <Button variant="outline" size="sm" onClick={() => setAuditEnvId(env.id)}>
                    <FileClock className="mr-2 h-4 w-4" />
                    View Logs
                  </Button>
                  {canManageEnv && (
                    <Button variant="outline" size="sm" onClick={() => { setEnvToSync(env.id); setSyncSourceEnv(''); }}>
                      <ArrowRightLeft className="mr-2 h-4 w-4" />
                      Sync Rules
                    </Button>
                  )}
                  <Button variant="default" size="sm" onClick={() => navigate(`/projects/${project.id}/environments/${env.id}`)}>
                    <Settings className="mr-2 h-4 w-4" />
                    Manage Details
                  </Button>
              </div>
            </CardHeader>
          </Card>
          );
        })}
      </div>

      <Dialog open={!!auditEnvId} onOpenChange={(open) => !open && setAuditEnvId(null)}>
        <DialogContent className="max-w-5xl max-h-[80vh] flex flex-col border-border/40 bg-zinc-950">
          <DialogHeader>
            <DialogTitle>Environment Activity Log</DialogTitle>
            <DialogDescription>
              Recent changes made to flags and settings in this environment.
            </DialogDescription>
          </DialogHeader>
          <div className="flex-1 overflow-hidden min-h-[400px]">
            {auditEnvId && <EnvironmentAuditLogs envId={auditEnvId} />}
          </div>
        </DialogContent>
      </Dialog>

      <Dialog open={!!envToSync} onOpenChange={(open) => !open && setEnvToSync(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Sync Rules from Another Environment</DialogTitle>
            <DialogDescription>
              This will overwrite all current flag rules in this environment with the rules from the selected source environment.
            </DialogDescription>
          </DialogHeader>
          <div className="py-4">
            <Select value={syncSourceEnv} onValueChange={setSyncSourceEnv}>
              <SelectTrigger>
                <SelectValue placeholder="Select source environment" />
              </SelectTrigger>
              <SelectContent>
                {project.environments
                  .filter(e => e.id !== envToSync)
                  .map(e => (
                    <SelectItem key={e.id} value={e.id}>{e.name}</SelectItem>
                  ))
                }
              </SelectContent>
            </Select>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEnvToSync(null)}>Cancel</Button>
            <Button onClick={handleSyncEnvironment} disabled={cloneEnvironment.isPending || !syncSourceEnv}>
              {cloneEnvironment.isPending ? 'Syncing...' : 'Sync Rules'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}