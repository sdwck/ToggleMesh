import { useState } from 'react';
import { Plus, Key, Box, RefreshCw, Copy, ArrowRightLeft, FileClock } from 'lucide-react';
import { useCreateEnvironment, useRotateEnvironmentKey, useCloneEnvironment, useAuditLogs } from '@/api/queries';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PaginationControls } from '@/components/ui/PaginationControls';
import { ProjectRole, type AuditLog, type Environment, type ProjectDetails } from '@/api/types';
import { toast } from 'sonner';
import { Skeleton } from '@/components/ui/skeleton';

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
                  <TableCell className="text-muted-foreground whitespace-nowrap">
                    {new Date(log.timestamp).toLocaleString()}
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline" className="text-[10px] font-mono uppercase">
                      {log.action}
                    </Badge>
                  </TableCell>
                  <TableCell className="font-mono text-xs text-primary/80">{log.entityName}</TableCell>
                  <TableCell className="text-muted-foreground truncate max-w-[120px]" title={log.performedBy}>
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
  const createEnvironment = useCreateEnvironment(project.id);
  const cloneEnvironment = useCloneEnvironment(project.id);
  const rotateKey = useRotateEnvironmentKey(project.id);

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [newEnvName, setNewEnvName] = useState('');

  const [isRotateConfirmOpen, setIsRotateConfirmOpen] = useState(false);
  const [envToRotate, setEnvToRotate] = useState<string | null>(null);
  const [keyToCopy, setKeyToCopy] = useState<string | null>(null);
  const [isKeyDialogOpen, setIsKeyDialogOpen] = useState(false);

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

  const confirmRotateKey = (envId: string) => {
    setEnvToRotate(envId);
    setIsRotateConfirmOpen(true);
  };

  const executeRotateKey = async () => {
    if (!envToRotate) return;
    try {
      const response = await rotateKey.mutateAsync(envToRotate);
      setIsRotateConfirmOpen(false);
      setKeyToCopy(response.apiKey);
      setIsKeyDialogOpen(true);
      toast.success('API Key rotated');
    } catch {
      toast.error('Failed to rotate API Key');
    }
  };

  const copyToClipboard = () => {
    if (keyToCopy) {
      navigator.clipboard.writeText(keyToCopy);
      toast.success('API Key copied to clipboard');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-end">
        {canManageProject && (
          <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
            <DialogTrigger asChild>
              <Button>
                <Plus className="mr-2 h-4 w-4" />
                New Environment
              </Button>
            </DialogTrigger>
            <DialogContent>
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
          <Card key={env.id} className="border-border/40">
            <CardHeader className="flex flex-row items-center justify-between pb-2 space-y-0">
              <div className="space-y-1">
                <CardTitle className="text-lg flex items-center gap-2">
                  <Box className="h-5 w-5 text-muted-foreground" />
                  {env.name}
                </CardTitle>
              </div>
            </CardHeader>
            <CardContent>
              <div className="mt-4 flex items-center justify-between px-6 py-4 rounded-lg bg-muted/30 border border-border/40">
                <div className="flex items-center gap-4">
                  <div className="h-10 w-10 rounded-full bg-primary/10 flex items-center justify-center">
                    <Key className="h-5 w-5 text-primary" />
                  </div>
                  <div>
                    <p className="text-sm font-medium">Environment API Key</p>
                    <p className="text-xs text-muted-foreground font-mono">
                      {env.keys[0]?.keyPrefix || 'No active key'}
                    </p>
                  </div>
                </div>
                <div className="flex gap-2">
                  <Button variant="outline" size="sm" onClick={() => setAuditEnvId(env.id)}>
                    <FileClock className="mr-2 h-4 w-4" />
                    View Logs
                  </Button>
                  {canManageEnv && (
                    <>
                      <Button variant="outline" size="sm" onClick={() => confirmRotateKey(env.id)}>
                        <RefreshCw className="mr-2 h-4 w-4" />
                        Rotate Key
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => { setEnvToSync(env.id); setSyncSourceEnv(''); }}>
                        <ArrowRightLeft className="mr-2 h-4 w-4" />
                        Sync Rules
                      </Button>
                    </>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>
        )})}
        {project.environments.length === 0 && (
          <Card className="border-border/40 p-8 flex flex-col items-center justify-center text-center">
            <Box className="h-12 w-12 text-muted-foreground/50 mb-4" />
            <h3 className="text-lg font-medium">No environments</h3>
            <p className="text-muted-foreground text-sm max-w-sm mt-1">
              Create an environment to start managing feature flags for this project.
            </p>
          </Card>
        )}
      </div>

      <Dialog open={!!auditEnvId} onOpenChange={(open) => !open && setAuditEnvId(null)}>
        <DialogContent className="max-w-4xl">
          <DialogHeader>
            <DialogTitle>Environment Audit Logs</DialogTitle>
            <DialogDescription>
              Recent changes and flag evaluations within this environment.
            </DialogDescription>
          </DialogHeader>
          <div className="py-2">
            {auditEnvId && <EnvironmentAuditLogs envId={auditEnvId} />}
          </div>
          <DialogFooter>
            <Button onClick={() => setAuditEnvId(null)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!envToSync} onOpenChange={(open) => !open && setEnvToSync(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Synchronize Rules</DialogTitle>
            <DialogDescription>
              Copy feature flag states and targeting rules from another environment.
              <br/><br/>
              <strong className="text-destructive">Warning:</strong> This will overwrite all existing configuration in <strong>{project.environments.find(e => e.id === envToSync)?.name}</strong>.
            </DialogDescription>
          </DialogHeader>
          <div className="py-4">
            <Select value={syncSourceEnv} onValueChange={setSyncSourceEnv}>
              <SelectTrigger>
                <SelectValue placeholder="Select source environment..." />
              </SelectTrigger>
              <SelectContent>
                {project.environments.filter((e: Environment) => e.id !== envToSync).map((e: Environment) => (
                  <SelectItem key={e.id} value={e.id}>{e.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEnvToSync(null)}>Cancel</Button>
            <Button 
              onClick={handleSyncEnvironment} 
              disabled={cloneEnvironment.isPending || !syncSourceEnv}
            >
              {cloneEnvironment.isPending ? 'Syncing...' : 'Sync Rules'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={isRotateConfirmOpen} onOpenChange={setIsRotateConfirmOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Rotate API Key</DialogTitle>
            <DialogDescription>
              Are you sure? The old API key for this environment will be instantly invalidated and any applications using it will be disconnected.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="mt-4">
            <Button variant="outline" onClick={() => setIsRotateConfirmOpen(false)}>Cancel</Button>
            <Button variant="destructive" onClick={executeRotateKey} disabled={rotateKey.isPending}>
              {rotateKey.isPending ? 'Rotating...' : 'Yes, Rotate Key'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={isKeyDialogOpen} onOpenChange={setIsKeyDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New API Key Generated</DialogTitle>
            <DialogDescription className="text-destructive font-medium mt-2">
              Please copy this key immediately. You will not be able to see it again.
            </DialogDescription>
          </DialogHeader>
          <div className="flex items-center gap-2 mt-4">
            <Input value={keyToCopy || ''} readOnly className="font-mono text-sm bg-muted/50" />
            <Button variant="secondary" onClick={copyToClipboard}>
              <Copy className="h-4 w-4" />
            </Button>
          </div>
          <DialogFooter className="mt-6">
            <Button onClick={() => setIsKeyDialogOpen(false)}>I have copied the key</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}