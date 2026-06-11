import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useProjectDetails, useRotateEnvironmentKey } from '@/api/queries';
import { ProjectRole, KeyType } from '@/api/types';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ArrowLeft, Key, RefreshCw, Copy } from 'lucide-react';
import { toast } from 'sonner';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';

export function EnvironmentDetailsPage() {
  const { projectId, environmentId } = useParams<{ projectId: string; environmentId: string }>();
  const navigate = useNavigate();
  const { data: project, isLoading } = useProjectDetails(projectId!);
  const rotateKey = useRotateEnvironmentKey(projectId!);

  const [isRotateConfirmOpen, setIsRotateConfirmOpen] = useState(false);
  const [rotatingKeyType, setRotatingKeyType] = useState<'server' | 'client' | null>(null);
  
  const [isKeyDialogOpen, setIsKeyDialogOpen] = useState(false);
  const [keyToCopy, setKeyToCopy] = useState<string | null>(null);

  if (isLoading) {
    return <div className="p-8 space-y-6"><Skeleton className="h-10 w-1/3" /><Skeleton className="h-64 w-full" /></div>;
  }

  if (!project) {
    return <div className="p-8 text-center text-muted-foreground">Project not found</div>;
  }

  const environment = project.environments?.find(e => e.id === environmentId);

  if (!environment) {
    return <div className="p-8 text-center text-muted-foreground">Environment not found</div>;
  }

  const canManageKeys = project.userRole === ProjectRole.Owner || project.userRole === ProjectRole.Admin;

  const confirmRotateKey = (type: 'server' | 'client') => {
    setRotatingKeyType(type);
    setIsRotateConfirmOpen(true);
  };

  const executeRotateKey = async () => {
    if (!rotatingKeyType) return;
    try {
      const response = await rotateKey.mutateAsync({ envId: environment.id, keyType: rotatingKeyType });
      setIsRotateConfirmOpen(false);
      setKeyToCopy(response.apiKey);
      setIsKeyDialogOpen(true);
      toast.success(`${rotatingKeyType === 'server' ? 'Server' : 'Client'} API Key rotated`);
    } catch {
      toast.error(`Failed to rotate API Key`);
    } finally {
      setRotatingKeyType(null);
    }
  };

  const copyToClipboard = () => {
    if (keyToCopy) {
      navigator.clipboard.writeText(keyToCopy);
      toast.success('API Key copied to clipboard');
    }
  };

  const serverKey = environment.keys.find(k => k.keyType === KeyType.Server);
  const clientKey = environment.keys.find(k => k.keyType === KeyType.Client);

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => navigate(`/projects/${projectId}?tab=environments`)}>
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold tracking-tight">{environment.name}</h1>
          <p className="text-muted-foreground">Environment Details & Settings</p>
        </div>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        <Card className="flex flex-col h-full">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Key className="h-5 w-5" />
              Server API Key
            </CardTitle>
            <CardDescription>
              Used in backend SDKs. Grants full access to evaluate feature flags. Keep this secure.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 mt-auto">
            <div className="bg-muted p-3 rounded-md font-mono text-sm flex items-center justify-between">
              <span>{serverKey ? serverKey.keyPrefix : 'No active server key'}</span>
            </div>
            {canManageKeys && (
              <Button variant="outline" className="w-full" onClick={() => confirmRotateKey('server')}>
                <RefreshCw className="mr-2 h-4 w-4" />
                Rotate Server Key
              </Button>
            )}
          </CardContent>
        </Card>

        <Card className="flex flex-col h-full">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Key className="h-5 w-5" />
              Client API Key
            </CardTitle>
            <CardDescription>
              Used in frontend/mobile SDKs. Only evaluates flags marked as "Client-Side Exposed".
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 mt-auto">
            <div className="bg-muted p-3 rounded-md font-mono text-sm flex items-center justify-between">
              <span>{clientKey ? clientKey.keyPrefix : 'No active client key'}</span>
            </div>
            {canManageKeys && (
              <Button variant="outline" className="w-full" onClick={() => confirmRotateKey('client')}>
                <RefreshCw className="mr-2 h-4 w-4" />
                Rotate Client Key
              </Button>
            )}
          </CardContent>
        </Card>
      </div>

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