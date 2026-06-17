import {useState} from 'react';
import {useParams, useNavigate} from 'react-router-dom';
import {
    useProjectDetails,
    useEnvironmentKeys,
    useCreateEnvironmentKey,
    useRevokeEnvironmentKey,
    useUpdateEnvironment,
    useDeleteEnvironment
} from '@/api/queries';
import {ProjectRole, KeyType} from '@/api/types';
import {Button} from '@/components/ui/button';
import {Table, TableBody, TableCell, TableHead, TableHeader, TableRow} from '@/components/ui/table';
import {ArrowLeft, Key, Plus, Copy, Trash2, MoreHorizontal, Code, Edit2, AlertTriangle} from 'lucide-react';
import {toast} from 'sonner';
import {Skeleton} from '@/components/ui/skeleton';
import {Input} from '@/components/ui/input';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle
} from '@/components/ui/dialog';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select';
import {Badge} from '@/components/ui/badge';
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card';
import {Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle} from '@/components/ui/sheet';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import {DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger} from '@/components/ui/dropdown-menu';

const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const pad = (num: number) => String(num).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
};

export function EnvironmentDetailsPage() {
    const {projectId, environmentId} = useParams<{ projectId: string; environmentId: string }>();
    const navigate = useNavigate();

    const {data: project, isLoading: isProjectLoading} = useProjectDetails(projectId!);
    const {data: keys, isLoading: isKeysLoading} = useEnvironmentKeys(projectId!, environmentId!);

    const createKey = useCreateEnvironmentKey(projectId!, environmentId!);
    const revokeKey = useRevokeEnvironmentKey(projectId!, environmentId!);

    const updateEnvironment = useUpdateEnvironment(projectId!, environmentId!);
    const deleteEnvironment = useDeleteEnvironment(projectId!);

    const [isCreateOpen, setIsCreateOpen] = useState(false);
    const [keyName, setKeyName] = useState('');
    const [keyType, setKeyType] = useState<KeyType>(KeyType.Server);

    const [isRevealOpen, setIsRevealOpen] = useState(false);
    const [plainKeyRevealed, setPlainKeyRevealed] = useState('');

    const [isRevokeConfirmOpen, setIsRevokeConfirmOpen] = useState(false);
    const [keyIdToRevoke, setKeyIdToRevoke] = useState<string | null>(null);

    const [isSdkSetupOpen, setIsSdkSetupOpen] = useState(false);

    const [isRenameOpen, setIsRenameOpen] = useState(false);
    const [envNameInput, setEnvNameInput] = useState('');

    const [isDeleteOpen, setIsDeleteOpen] = useState(false);
    const [envDeleteInput, setEnvDeleteInput] = useState('');

    const canManageKeys = project?.userRole === ProjectRole.Owner || project?.userRole === ProjectRole.Admin;

    const handleCreateKeySubmit = async () => {
        if (!keyName.trim()) {
            toast.error('Key name is required');
            return;
        }

        try {
            const response = await createKey.mutateAsync({name: keyName, type: keyType});
            setIsCreateOpen(false);
            setKeyName('');
            setPlainKeyRevealed(response.plainKey);
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

    const copyText = (text: string) => {
        navigator.clipboard.writeText(text);
        toast.success('Copied to clipboard');
    };

    const handleSaveName = async () => {
        if (!envNameInput.trim()) {
            toast.error('Environment name cannot be empty');
            return;
        }
        try {
            await updateEnvironment.mutateAsync(envNameInput);
            setIsRenameOpen(false);
            toast.success('Environment renamed successfully');
        } catch {
            toast.error('Failed to rename environment');
        }
    };

    const handleDeleteEnv = async () => {
        try {
            await deleteEnvironment.mutateAsync(environmentId!);
            setIsDeleteOpen(false);
            toast.success('Environment deleted successfully');
            navigate(`/projects/${projectId}/environments`);
        } catch (err: any) {
            const errorMsg = err.response?.data?.errors?.[0]?.message || 'Failed to delete environment';
            toast.error(errorMsg);
        }
    };

    const environment = project?.environments?.find(e => e.id === environmentId);

    if (!isProjectLoading && (!project || !environment)) {
        return (
            <div
                className="p-8 text-center text-muted-foreground flex flex-col items-center justify-center min-h-[400px]">
                <AlertTriangle className="h-10 w-10 text-destructive mb-4"/>
                <h3 className="font-semibold text-lg text-zinc-200">Environment not found</h3>
                <p className="text-sm mt-1">The environment or project you are trying to access does not exist.</p>
                <Button variant="outline" className="mt-6" onClick={() => navigate('/projects')}>
                    Back to Projects
                </Button>
            </div>
        );
    }

    const serverKey = keys?.find(k => k.keyType === KeyType.Server)?.keyPreview || "tm_server_your_key_here";
    const clientKey = keys?.find(k => k.keyType === KeyType.Client)?.keyPreview || "tm_client_your_key_here";

    const csharpRegisterCode = `builder.Services.AddToggleMeshClient(options => {
    options.BaseUrl = "${window.location.origin}";
    options.ApiKey = "${serverKey}";
});

// Automatically resolves UserId, Email, and Roles from HttpContext
builder.Services.AddToggleMeshHttpContext();`;

    const csharpUsageCode = `public class CheckoutService {
    private readonly IToggleMeshClient _toggleMesh;

    public CheckoutService(IToggleMeshClient toggleMesh) {
        _toggleMesh = toggleMesh;
    }

    public void ProcessPayment() {
        if (_toggleMesh.IsEnabled("new-checkout-flow")) {
            // ...
        }
    }
}`;

    const typescriptRegisterCode = `
import { ToggleMeshClient } from 'togglemesh-js';
import { ToggleMeshProvider } from 'togglemesh-js/react';

const client = new ToggleMeshClient({
    baseUrl: "${window.location.origin}",
    clientKey: "${clientKey}",
    refreshInterval: 30
});

await client.identify("user_123", { 
    Country: "US", 
    Plan: "Pro" 
});

ReactDOM.createRoot(document.getElementById('root')!).render(
  <ToggleMeshProvider client={client}>
    <App />
  </ToggleMeshProvider>
);`;

    const typescriptUsageCode = `import { useFeatureFlag } from 'togglemesh-js/react';

export function PaymentComponent() {
    const showPaypal = useFeatureFlag('new-checkout-flow');
    return showPaypal ? <PayPalButton /> : <CreditCardButton />;
}`;

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div className="flex items-center gap-4">
                    <Button variant="ghost" size="icon" onClick={() => navigate(`/projects/${projectId}/environments`)}
                            className="cursor-pointer">
                        <ArrowLeft className="h-4 w-4"/>
                    </Button>
                    <div>
                        <h1 className="text-3xl font-bold tracking-tight h-9 flex items-center gap-2">
                            {isProjectLoading ? (
                                <Skeleton className="h-8 w-48"/>
                            ) : (
                                <>
                                    {environment?.name}
                                    {canManageKeys && (
                                        <DropdownMenu>
                                            <DropdownMenuTrigger asChild>
                                                <Button variant="ghost" size="icon"
                                                        className="h-8 w-8 text-muted-foreground hover:text-foreground cursor-pointer rounded-md">
                                                    <MoreHorizontal className="h-4 w-4"/>
                                                </Button>
                                            </DropdownMenuTrigger>
                                            <DropdownMenuContent align="start"
                                                                 className="border-border/40 bg-zinc-950 w-44">
                                                <DropdownMenuItem onClick={() => {
                                                    setEnvNameInput(environment?.name || '');
                                                    setIsRenameOpen(true);
                                                }} className="cursor-pointer">
                                                    <Edit2 className="mr-2 h-4 w-4"/> Rename
                                                </DropdownMenuItem>
                                                <DropdownMenuItem onClick={() => {
                                                    setEnvDeleteInput('');
                                                    setIsDeleteOpen(true);
                                                }} className="text-destructive focus:text-destructive cursor-pointer">
                                                    <Trash2 className="mr-2 h-4 w-4"/> Delete
                                                </DropdownMenuItem>
                                            </DropdownMenuContent>
                                        </DropdownMenu>
                                    )}
                                </>
                            )}
                        </h1>
                        <p className="text-muted-foreground">Manage keys and credentials for this environment</p>
                    </div>
                </div>

                <div className="flex items-center gap-2">
                    <Button variant="outline" onClick={() => setIsSdkSetupOpen(true)} className="cursor-pointer">
                        <Code className="mr-2 h-4 w-4"/>
                        SDK Setup
                    </Button>
                    {canManageKeys && (
                        <Button onClick={() => setIsCreateOpen(true)} className="cursor-pointer">
                            <Plus className="mr-2 h-4 w-4"/>
                            Create Key
                        </Button>
                    )}
                </div>
            </div>

            <Card className="border-border/40 bg-zinc-950/20">
                <CardHeader>
                    <CardTitle className="flex items-center gap-2 text-lg">
                        <Key className="h-5 w-5 text-primary"/>
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
                                <TableHead>Last Used</TableHead>
                                {canManageKeys && <TableHead className="text-right w-[80px]">Actions</TableHead>}
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {isKeysLoading ? (
                                Array.from({length: 3}).map((_, i) => (
                                    <TableRow key={i} className="h-[53px] border-border/40">
                                        <TableCell><Skeleton className="h-5 w-[150px] rounded"/></TableCell>
                                        <TableCell><Skeleton className="h-5 w-16 rounded"/></TableCell>
                                        <TableCell><Skeleton className="h-5 w-32 rounded font-mono"/></TableCell>
                                        <TableCell><Skeleton className="h-5 w-24 rounded"/></TableCell>
                                        <TableCell><Skeleton className="h-5 w-24 rounded"/></TableCell>
                                        {canManageKeys && (
                                            <TableCell className="text-right">
                                                <MoreHorizontal
                                                    className="h-4 w-4 text-zinc-800 ml-auto animate-pulse"/>
                                            </TableCell>
                                        )}
                                    </TableRow>
                                ))
                            ) : keys && keys.length > 0 ? (
                                keys.map((key) => (
                                    <TableRow key={key.id}
                                              className="border-border/40 hover:bg-muted/10 h-[53px] group">
                                        <TableCell className="font-medium">{key.name}</TableCell>
                                        <TableCell>
                                            <Badge variant={key.keyType === KeyType.Server ? 'default' : 'secondary'}>
                                                {key.keyType === KeyType.Server ? 'Server' : 'Client'}
                                            </Badge>
                                        </TableCell>
                                        <TableCell
                                            className="font-mono text-xs text-muted-foreground">{key.keyPreview}</TableCell>
                                        <TableCell className="text-muted-foreground text-xs font-mono">
                                            {formatDate(key.createdOn)}
                                        </TableCell>
                                        <TableCell className="text-muted-foreground text-xs font-mono">
                                            {key.expireOn ? formatDate(key.expireOn) : (key.lastUsedAt ? formatDate(key.lastUsedAt) : 'Never')}
                                        </TableCell>
                                        {canManageKeys && (
                                            <TableCell className="text-right">
                                                <Button
                                                    variant="ghost"
                                                    size="icon"
                                                    className="h-8 w-8 text-muted-foreground hover:text-destructive hover:bg-muted/50 rounded-md cursor-pointer"
                                                    onClick={() => handleRevokeConfirm(key.id)}
                                                >
                                                    <Trash2 className="h-4 w-4"/>
                                                </Button>
                                            </TableCell>
                                        )}
                                    </TableRow>
                                ))
                            ) : (
                                <TableRow>
                                    <TableCell colSpan={canManageKeys ? 6 : 5}
                                               className="h-24 text-center text-muted-foreground">
                                        No active API keys found. Create a key to get started.
                                    </TableCell>
                                </TableRow>
                            )}
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>

            <Sheet open={isSdkSetupOpen} onOpenChange={setIsSdkSetupOpen}>
                <SheetContent className="w-full sm:max-w-xl overflow-y-auto bg-zinc-950 border-border/40">
                    <SheetHeader>
                        <SheetTitle>SDK Integration Guide</SheetTitle>
                        <SheetDescription>Copy and paste these snippets to integrate ToggleMesh into your
                            app.</SheetDescription>
                    </SheetHeader>

                    <Tabs defaultValue="csharp" className="mt-6 space-y-4">
                        <TabsList className="bg-zinc-900 border border-border/10 h-10">
                            <TabsTrigger value="csharp" className="text-xs">.NET SDK</TabsTrigger>
                            <TabsTrigger value="typescript" className="text-xs">React/TS SDK</TabsTrigger>
                            <TabsTrigger value="cli-net" className="text-xs">CLI Tool (.NET)</TabsTrigger>
                            <TabsTrigger value="cli-js" className="text-xs">CLI Tool (JS)</TabsTrigger>
                            {/*TODO: Move to the separate guide*/}
                        </TabsList>

                        <TabsContent value="csharp" className="space-y-4">
                            <div className="space-y-1">
                                <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install
                                    Package</h4>
                                <pre
                                    className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    dotnet add package ToggleMesh.SDK
                                </pre>
                            </div>
                            <div className="space-y-1">
                                <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Register
                                    Client</h4>
                                <div className="relative">
                                    <pre
                                        className="bg-zinc-900/60 p-4 rounded-md font-mono text-xs overflow-auto border border-border/10 text-emerald-400">
                                        {csharpRegisterCode}
                                    </pre>
                                    <Button variant="ghost" size="icon" onClick={() => copyText(csharpRegisterCode)}
                                            className="absolute top-2 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                        <Copy className="h-3.5 w-3.5"/>
                                    </Button>
                                </div>
                                <div className="space-y-1">
                                    <h4 className="text-xs font-semibold text-muted-foreground uppercase">3. Use
                                        Client</h4>
                                    <div className="relative">
                                    <pre
                                        className="bg-zinc-900/60 p-4 rounded-md font-mono text-xs overflow-auto border border-border/10 text-emerald-400">
                                        {csharpUsageCode}
                                    </pre>
                                        <Button variant="ghost" size="icon" onClick={() => copyText(csharpUsageCode)}
                                                className="absolute top-2 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                            <Copy className="h-3.5 w-3.5"/>
                                        </Button>
                                    </div>
                                </div>
                            </div>
                        </TabsContent>

                        <TabsContent value="typescript" className="space-y-4">
                            <div className="space-y-1">
                                <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install
                                    Package</h4>
                                <pre
                                    className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    npm install togglemesh-js 
                                </pre>
                            </div>
                            <div className="space-y-1">
                                <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Initialize
                                    Client</h4>
                                <div className="relative">
                                    <pre
                                        className="bg-zinc-900/60 p-4 rounded-md font-mono text-xs overflow-auto border border-border/10 text-emerald-400">
                                        {typescriptRegisterCode}
                                    </pre>
                                    <Button variant="ghost" size="icon" onClick={() => copyText(typescriptRegisterCode)}
                                            className="absolute top-2 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                        <Copy className="h-3.5 w-3.5"/>
                                    </Button>
                                </div>
                            </div>
                            <div className="space-y-1">
                                <h4 className="text-xs font-semibold text-muted-foreground uppercase">3. Use Client</h4>
                                <div className="relative">
                                    <pre
                                        className="bg-zinc-900/60 p-4 rounded-md font-mono text-xs overflow-auto border border-border/10 text-emerald-400">
                                        {typescriptUsageCode}
                                    </pre>
                                    <Button variant="ghost" size="icon" onClick={() => copyText(typescriptUsageCode)}
                                            className="absolute top-2 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                        <Copy className="h-3.5 w-3.5"/>
                                    </Button>
                                </div>
                            </div>
                        </TabsContent>

                        <TabsContent value="cli-net" className="space-y-4">
                            <div className="space-y-1">
                                <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install CLI
                                    Tool Globally</h4>
                                <div className="relative">
                                    <pre
                                        className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                        dotnet tool install -g ToggleMesh.CLI
                                    </pre>
                                    <Button variant="ghost" size="icon"
                                            onClick={() => copyText("dotnet tool install -g ToggleMesh.CLI")}
                                            className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                        <Copy className="h-3.5 w-3.5"/>
                                    </Button>
                                </div>
                            </div>

                            <div className="space-y-1">
                                <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Run Interactive
                                    Configuration</h4>
                                <div className="relative">
                                    <pre
                                        className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                        togglemesh config
                                    </pre>
                                    <Button variant="ghost" size="icon" onClick={() => copyText("togglemesh config")}
                                            className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                        <Copy className="h-3.5 w-3.5"/>
                                    </Button>
                                </div>
                                <p className="text-[10px] text-muted-foreground pl-1">
                                    Follow the prompts to configure your credentials and target project setup.
                                </p>
                            </div>

                            <div className="space-y-1">
                                <h4 className="text-xs font-semibold text-muted-foreground uppercase">3. Synchronize
                                    Feature Flags</h4>
                                <div className="relative">
                                    <pre
                                        className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                        togglemesh sync
                                    </pre>
                                    <Button variant="ghost" size="icon" onClick={() => copyText("togglemesh sync")}
                                            className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                        <Copy className="h-3.5 w-3.5"/>
                                    </Button>
                                </div>
                            </div>
                        </TabsContent>

                        <TabsContent value="cli-js" className="space-y-4">
                            <div className="space-y-1">
                                <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install CLI as Dev Dependency (NPM)</h4>
                                <div className="relative">
                                    <pre className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                        npm install -D togglemesh
                                    </pre>
                                    <Button variant="ghost" size="icon" onClick={() => copyText("npm install -D togglemesh")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                        <Copy className="h-3.5 w-3.5" />
                                    </Button>
                                </div>
                            </div>

                            <div className="space-y-1">
                                <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Run Interactive Configuration</h4>
                                <div className="relative">
                                    <pre className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                        npx togglemesh config
                                    </pre>
                                    <Button variant="ghost" size="icon" onClick={() => copyText("npx togglemesh config")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                        <Copy className="h-3.5 w-3.5" />
                                    </Button>
                                </div>
                                <p className="text-[10px] text-muted-foreground pl-1">
                                    Follow the prompts to configure your credentials and target project setup.
                                </p>
                            </div>

                            <div className="space-y-1">
                                <h4 className="text-xs font-semibold text-muted-foreground uppercase">3. Synchronize Feature Flags</h4>
                                <div className="relative">
                                    <pre className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                        npx togglemesh sync
                                    </pre>
                                    <Button variant="ghost" size="icon" onClick={() => copyText("npx togglemesh sync")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                        <Copy className="h-3.5 w-3.5" />
                                    </Button>
                                </div>
                            </div>
                        </TabsContent>
                    </Tabs>
                </SheetContent>
            </Sheet>

            <Dialog open={isRenameOpen} onOpenChange={setIsRenameOpen}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle>Rename Environment</DialogTitle>
                        <DialogDescription>Change the visible name of this environment.</DialogDescription>
                    </DialogHeader>
                    <div className="py-4">
                        <Input
                            value={envNameInput}
                            onChange={(e) => setEnvNameInput(e.target.value)}
                            className="bg-zinc-950/20"
                            autoFocus
                        />
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setIsRenameOpen(false)}>Cancel</Button>
                        <Button onClick={handleSaveName} disabled={updateEnvironment.isPending}>Save</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            <Dialog open={isDeleteOpen} onOpenChange={setIsDeleteOpen}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle className="text-destructive flex items-center gap-2">
                            <AlertTriangle className="h-5 w-5"/> Delete Environment
                        </DialogTitle>
                        <DialogDescription>
                            This will permanently delete the environment, all of its flag configurations, and active API
                            keys.
                            This action cannot be undone.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-4 py-4">
                        <p className="text-sm text-muted-foreground">
                            To confirm deletion, type <strong
                            className="text-foreground font-mono">{environment?.name}</strong> below:
                        </p>
                        <Input
                            placeholder="Type environment name to confirm"
                            value={envDeleteInput}
                            onChange={(e) => setEnvDeleteInput(e.target.value)}
                            className="border-destructive/30 focus-visible:ring-destructive bg-zinc-950/20"
                        />
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setIsDeleteOpen(false)}>Cancel</Button>
                        <Button
                            variant="destructive"
                            onClick={handleDeleteEnv}
                            disabled={deleteEnvironment.isPending || envDeleteInput !== environment?.name}
                            className="cursor-pointer"
                        >
                            Delete Environment
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

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
                                    <SelectValue placeholder="Select key type"/>
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value={KeyType.Server.toString()}>Server (Backend
                                        Evaluation)</SelectItem>
                                    <SelectItem value={KeyType.Client.toString()}>Client (Frontend
                                        Evaluation)</SelectItem>
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
                        <Input value={plainKeyRevealed} readOnly className="font-mono text-sm bg-muted/50"/>
                        <Button variant="secondary" size="icon" onClick={copyToClipboard}>
                            <Copy className="h-4 w-4"/>
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
                            Are you sure? This action cannot be undone. Any applications using this key will immediately
                            lose access.
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