import { useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { Plus, Key, Box, RefreshCw, Copy, ArrowLeft, Users } from 'lucide-react';
import { useProjectDetails, useCreateEnvironment, useRotateEnvironmentKey, useProjectMembers, useAddProjectMember } from '@/api/queries';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ProjectRole } from '@/api/types';
import { toast } from 'sonner';

import { Skeleton } from '@/components/ui/skeleton';

export function ProjectDetailsPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const navigate = useNavigate();
  const { data: project, isLoading } = useProjectDetails(projectId!);
  const { data: members, isLoading: isLoadingMembers } = useProjectMembers(projectId!);
  const createEnvironment = useCreateEnvironment(projectId!);
  const addMember = useAddProjectMember(projectId!);
  
  const [rotatingEnvId, setRotatingEnvId] = useState<string | null>(null);
  const [isRotateConfirmOpen, setIsRotateConfirmOpen] = useState(false);
  const [envToRotate, setEnvToRotate] = useState<string | null>(null);

  const rotateKey = useRotateEnvironmentKey(projectId!, rotatingEnvId || '');
  const [keyToCopy, setKeyToCopy] = useState<string | null>(null);
  const [isKeyDialogOpen, setIsKeyDialogOpen] = useState(false);

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [isAddMemberOpen, setIsAddMemberOpen] = useState(false);
  
  const [newEnvName, setNewEnvName] = useState('');
  const [newMemberEmail, setNewMemberEmail] = useState('');
  const [newMemberRole, setNewMemberRole] = useState<string>('3');

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

  const handleAddMember = async () => {
    if (!newMemberEmail.trim()) return;
    try {
      await addMember.mutateAsync({ email: newMemberEmail, role: parseInt(newMemberRole) });
      toast.success('Member added');
      setNewMemberEmail('');
      setNewMemberRole('3');
      setIsAddMemberOpen(false);
    } catch {
      toast.error('Failed to add member');
    }
  };

  const confirmRotateKey = (envId: string) => {
    setEnvToRotate(envId);
    setIsRotateConfirmOpen(true);
  };

  const executeRotateKey = async () => {
    if (!envToRotate) return;
    try {
      setRotatingEnvId(envToRotate);
      setIsRotateConfirmOpen(false);
      setTimeout(async () => {
        const response = await rotateKey.mutateAsync();
        setKeyToCopy(response.apiKey);
        setIsKeyDialogOpen(true);
        toast.success('API Key rotated');
      }, 0);
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

  const getRoleName = (role: ProjectRole) => {
    switch (role) {
      case ProjectRole.Owner: return 'Owner';
      case ProjectRole.Admin: return 'Admin';
      case ProjectRole.Editor: return 'Editor';
      case ProjectRole.Viewer: return 'Viewer';
      default: return 'Unknown';
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-2">
          <Skeleton className="h-8 w-8" />
          <Skeleton className="h-8 w-32" />
        </div>
        <div>
          <Skeleton className="h-8 w-64 mb-2" />
          <Skeleton className="h-4 w-96" />
        </div>
        <Skeleton className="h-10 w-64 mt-6" />
        <div className="grid gap-6 mt-6">
          <Skeleton className="h-[120px] w-full" />
          <Skeleton className="h-[120px] w-full" />
        </div>
      </div>
    );
  }

  if (!project) return <div>Project not found</div>;

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" className="-ml-3 text-muted-foreground" onClick={() => navigate('/projects')}>
        <ArrowLeft className="mr-2 h-4 w-4" />
        Back to Projects
      </Button>

      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">{project.name}</h2>
          <p className="text-muted-foreground">Manage environments and team members.</p>
        </div>
      </div>

      <Tabs defaultValue="environments" className="space-y-6">
        <TabsList>
          <TabsTrigger value="environments">Environments</TabsTrigger>
          <TabsTrigger value="members">Members</TabsTrigger>
        </TabsList>

        <TabsContent value="environments" className="space-y-6">
          <div className="flex justify-end">
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
          </div>

          <div className="grid gap-6">
            {project.environments.map((env) => (
              <Card key={env.id} className="border-border/40">
                <CardHeader className="flex flex-row items-center justify-between pb-2 space-y-0">
                  <div className="space-y-1">
                    <CardTitle className="text-lg flex items-center gap-2">
                      <Box className="h-5 w-5 text-muted-foreground" />
                      {env.name}
                    </CardTitle>
                  </div>
                  <Button variant="secondary" asChild>
                    <Link to={`/projects/${project.id}/environments/${env.id}`}>Manage Flags</Link>
                  </Button>
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
                    <Button variant="outline" size="sm" onClick={() => confirmRotateKey(env.id)}>
                      <RefreshCw className="mr-2 h-4 w-4" />
                      Rotate Key
                    </Button>
                  </div>
                </CardContent>
              </Card>
            ))}
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
        </TabsContent>

        <TabsContent value="members" className="space-y-6">
          <div className="flex justify-end">
            <Dialog open={isAddMemberOpen} onOpenChange={setIsAddMemberOpen}>
              <DialogTrigger asChild>
                <Button>
                  <Plus className="mr-2 h-4 w-4" />
                  Add Member
                </Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Add Team Member</DialogTitle>
                  <DialogDescription>
                    Invite someone to collaborate on this project.
                  </DialogDescription>
                </DialogHeader>
                <div className="space-y-4 py-4">
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Email Address</label>
                    <Input
                      placeholder="user@example.com"
                      value={newMemberEmail}
                      onChange={(e) => setNewMemberEmail(e.target.value)}
                    />
                  </div>
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Role</label>
                    <Select value={newMemberRole} onValueChange={setNewMemberRole}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select a role" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="0">Owner</SelectItem>
                        <SelectItem value="1">Admin</SelectItem>
                        <SelectItem value="2">Editor</SelectItem>
                        <SelectItem value="3">Viewer</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                </div>
                <DialogFooter>
                  <Button variant="outline" onClick={() => setIsAddMemberOpen(false)}>Cancel</Button>
                  <Button onClick={handleAddMember} disabled={addMember.isPending || !newMemberEmail.trim()}>
                    {addMember.isPending ? 'Adding...' : 'Add Member'}
                  </Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          </div>

          <Card className="border-border/40">
            <CardHeader>
              <CardTitle className="text-lg flex items-center gap-2">
                <Users className="h-5 w-5 text-muted-foreground" />
                Team Members
              </CardTitle>
              <CardDescription>Users who have access to this project.</CardDescription>
            </CardHeader>
            <CardContent>
              {isLoadingMembers ? (
                <div className="py-4 text-center text-sm text-muted-foreground">Loading members...</div>
              ) : (
                <div className="rounded-md border border-border/40 overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow className="hover:bg-transparent">
                        <TableHead>User</TableHead>
                        <TableHead>Role</TableHead>
                        <TableHead className="text-right">Actions</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {members?.map((member) => (
                        <TableRow key={member.id} className="hover:bg-muted/30">
                          <TableCell>
                            <div className="flex items-center gap-3">
                              <div className="h-8 w-8 rounded-full bg-primary/10 flex items-center justify-center">
                                <Users className="h-4 w-4 text-primary" />
                              </div>
                              <div className="flex flex-col">
                                <span className="font-medium">{member.email}</span>
                              </div>
                            </div>
                          </TableCell>
                          <TableCell>
                            <span className="inline-flex items-center rounded-md bg-secondary px-2 py-1 text-xs font-medium text-secondary-foreground">
                              {getRoleName(member.role)}
                            </span>
                          </TableCell>
                          <TableCell className="text-right">
                            <Button 
                              variant="ghost" 
                              size="sm" 
                              className="text-muted-foreground hover:text-foreground"
                              onClick={() => toast('Edit member role is not yet supported in the UI.')}
                            >
                              Edit
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                      {members?.length === 0 && (
                        <TableRow>
                          <TableCell colSpan={3} className="h-24 text-center text-muted-foreground">
                            No members found.
                          </TableCell>
                        </TableRow>
                      )}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

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
