import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useProjectDetails, useEnvironmentKeys, useCreateEnvironmentKey, useRevokeEnvironmentKey } from '@/api/queries';
import { ProjectRole, KeyType } from '@/api/types';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ArrowLeft, Key, Plus, Copy, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

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

export function EnvironmentDetailsPage() {
  const { projectId, environmentId } = useParams<{ projectId: string; environmentId: string }>();
  const navigate = useNavigate();

  const { data: project, isLoading: isProjectLoading } = useProjectDetails(projectId!);
  const { data: keys, isLoading: isKeysLoading } = useEnvironmentKeys(projectId!, environmentId!);

  const createKey = useCreateEnvironmentKey(projectId!, environmentId!);
  const revokeKey = useRevokeEnvironmentKey(projectId!, environmentId!);

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [keyName, setKeyName] = useState('');
  const [keyType, setKeyType] = useState<KeyType>(KeyType.Server);

  const [isRevealOpen, setIsRevealOpen] = useState(false);
  const [plainKeyRevealed, setPlainKeyReveal] = useState('');

  const [isRevokeConfirmOpen, setIsRevokeConfirmOpen] = useState(false);
  const [keyIdToRevoke, setKeyIdToRevoke] = useState<string | null>(null);

  if (isProjectLoading || isKeysLoading) {
    return (
      <div className="p-8 space-y-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (!project) {
    return <div className="p-8 text-center text-muted-foreground">Project not found</div>;
  }

  const environment = project.environments?.find(e => e.id === environmentId);

  if (!environment) {
    return <div className="p-8 text-center text-muted-foreground">Environment not found</div>;
  }

  const canManageKeys = project.userRole === ProjectRole.Owner || project.userRole === ProjectRole.Admin;

  const handleCreateKeySubmit = async () => {
    if (!keyName.trim()) {
      toast.error('Key name is required');
      return;
    }

    try {
      const response = await createKey.mutateAsync({ name: keyName, type: keyType });
      setIsCreateOpen(false);
      setKeyName('');
      setPlainKeyReveal(response.plainKey);
      setIsRevealOpen(true);
      toast.success('API Key created successfully');
    } catch {
      toast.error('Failed to create API Key');
    }
  };

  const handleRevokeConfirm = (keyId: string) => {
    setKeyIdToRevoke(keyId);
    setIsRevokeConfirmOpen(true);
  };

  const executeRevoke = async () => {
    if (!keyIdToRevoke) return;

    try {
      await revokeKey.mutateAsync(keyIdToRevoke);
      setIsRevokeConfirmOpen(false);
      setKeyIdToRevoke(null);
      toast.success('API Key revoked successfully');
    } catch {
      toast.error('Failed to revoke API Key');
    }
  };

  const copyToClipboard = () => {
    if (plainKeyRevealed) {
      navigator.clipboard.writeText(plainKeyRevealed);
      toast.success('API Key copied to clipboard');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => navigate(`/projects/${projectId}/environments`)}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">{environment.name}</h1>
            <p className="text-muted-foreground">Manage keys and credentials for this environment</p>
          </div>
        </div>

        {canManageKeys && (
          <Button onClick={() => setIsCreateOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Create Key
          </Button>
        )}
      </div>

      <Card className="border-border/40 bg-zinc-950/20">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg">
            <Key className="h-5 w-5 text-primary" />
            Environment API Keys
          </CardTitle>
          <CardDescription>
            Use Server keys in backend environments, and Client keys in frontend SDKs.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow className="border-border/40">
                <TableHead>Name</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Preview</TableHead>
                <TableHead>Created At</TableHead>
                {canManageKeys && <TableHead className="text-right">Actions</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {keys && keys.length > 0 ? (
                keys.map((key) => (
                  <TableRow key={key.id} className="border-border/40">
                    <TableCell className="font-medium">{key.name}</TableCell>
                    <TableCell>
                      <Badge variant={key.keyType === KeyType.Server ? 'default' : 'secondary'}>
                        {key.keyType === KeyType.Server ? 'Server' : 'Client'}
                      </Badge>
                    </TableCell>
                    <TableCell className="font-mono text-xs">{key.keyPreview}</TableCell>
                    <TableCell className="text-muted-foreground text-xs font-mono">
                      {formatDate(key.createdOn)}
                    </TableCell>
                    {canManageKeys && (
                      <TableCell className="text-right">
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 text-muted-foreground hover:text-foreground hover:bg-muted/50"
                          onClick={() => handleRevokeConfirm(key.id)}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </TableCell>
                    )}
                  </TableRow>
                ))
              ) : (
                <TableRow>
                  <TableCell colSpan={canManageKeys ? 5 : 4} className="h-24 text-center text-muted-foreground">
                    No active API keys found. Create a key to get started.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
        <DialogContent className="border-border/40 bg-zinc-950">
          <DialogHeader>
            <DialogTitle>Create API Key</DialogTitle>
            <DialogDescription>
              Assign a name and select a key scope type.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">Name</label>
              <Input
                placeholder="e.g. Production Backend"
                value={keyName}
                onChange={(e) => setKeyName(e.target.value)}
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">Scope Type</label>
              <Select
                value={keyType.toString()}
                onValueChange={(val) => setKeyType(parseInt(val, 10) as KeyType)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select key type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={KeyType.Server.toString()}>Server (Backend Evaluation)</SelectItem>
                  <SelectItem value={KeyType.Client.toString()}>Client (Frontend Evaluation)</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setIsCreateOpen(false)}>Cancel</Button>
            <Button onClick={handleCreateKeySubmit} disabled={createKey.isPending}>
              {createKey.isPending ? 'Creating...' : 'Create Key'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={isRevealOpen} onOpenChange={setIsRevealOpen}>
        <DialogContent className="border-border/40 bg-zinc-950">
          <DialogHeader>
            <DialogTitle>API Key Generated</DialogTitle>
            <DialogDescription className="text-destructive font-semibold mt-2">
              Warning: Copy this key now! You will never be shown this key again.
            </DialogDescription>
          </DialogHeader>

          <div className="flex items-center gap-2 mt-4">
            <Input value={plainKeyRevealed} readOnly className="font-mono text-sm bg-muted/50" />
            <Button variant="secondary" size="icon" onClick={copyToClipboard}>
              <Copy className="h-4 w-4" />
            </Button>
          </div>

          <DialogFooter className="mt-6">
            <Button className="w-full" onClick={() => setIsRevealOpen(false)}>
              I have safely copied this key
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={isRevokeConfirmOpen} onOpenChange={setIsRevokeConfirmOpen}>
        <DialogContent className="border-border/40 bg-zinc-950">
          <DialogHeader>
            <DialogTitle className="text-destructive">Revoke API Key</DialogTitle>
            <DialogDescription>
              Are you sure? This action cannot be undone. Any applications using this key will immediately lose access.
            </DialogDescription>
          </DialogHeader>

          <DialogFooter className="mt-4">
            <Button variant="outline" onClick={() => setIsRevokeConfirmOpen(false)}>Cancel</Button>
            <Button variant="destructive" onClick={executeRevoke} disabled={revokeKey.isPending}>
              {revokeKey.isPending ? 'Revoking...' : 'Yes, Revoke Key'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
