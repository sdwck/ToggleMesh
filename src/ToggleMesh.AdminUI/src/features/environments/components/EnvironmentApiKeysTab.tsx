import { useState } from 'react';
import { useEnvironmentKeys, useRevokeEnvironmentKey } from '@/api/queries';
import { KeyType } from '@/api/types';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Key, Plus, Trash2, MoreHorizontal, Terminal, Code } from 'lucide-react';
import { toast } from 'sonner';
import { CreateEnvironmentKeyDialog } from './CreateEnvironmentKeyDialog';
import { formatDate } from '@/utils/dateFormatter';
import { SdkIntegrationGuideSheet } from './SdkIntegrationGuideSheet';
import { CliIntegrationGuideSheet } from './CliIntegrationGuideSheet';
interface EnvironmentApiKeysTabProps {
    projectId: string;
    environmentId: string;
}

export function EnvironmentApiKeysTab({ projectId, environmentId }: EnvironmentApiKeysTabProps) {
    const { data: keys, isLoading: isKeysLoading } = useEnvironmentKeys(projectId, environmentId);
    const revokeKey = useRevokeEnvironmentKey(projectId, environmentId);

    const [isCreateOpen, setIsCreateOpen] = useState(false);
    const [isRevokeConfirmOpen, setIsRevokeConfirmOpen] = useState(false);
    const [isSdkSetupOpen, setIsSdkSetupOpen] = useState(false);
    const [isCliSetupOpen, setIsCliSetupOpen] = useState(false);
    const [keyIdToRevoke, setKeyIdToRevoke] = useState<string | null>(null);

    const serverKey = keys?.find(k => k.keyType === KeyType.Server)?.keyPreview || "tm_server_your_key_here";
    const clientKey = keys?.find(k => k.keyType === KeyType.Client)?.keyPreview || "tm_client_your_key_here";

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

    return (
        <div className="space-y-4">
            <Card className="border-border/40 bg-zinc-950/20">
                <CardHeader className="flex flex-col sm:flex-row items-start sm:items-center justify-between space-y-4 sm:space-y-0 pb-4 border-b border-border/40">
                    <div>
                        <CardTitle className="text-base font-semibold flex items-center gap-2">
                            <Key className="h-4 w-4 text-primary" />
                            Environment API Keys
                        </CardTitle>
                        <CardDescription className="mt-1">
                            Use Server keys in backend environments, and Client keys in frontend SDKs.
                        </CardDescription>
                    </div>
                    <div className="flex flex-wrap gap-2 w-full sm:w-auto">
                        <Button variant="outline" size="sm" onClick={() => setIsCliSetupOpen(true)} className="cursor-pointer text-xs h-8 flex-1 sm:flex-none">
                            <Terminal className="mr-2 h-3.5 w-3.5" />
                            CLI
                        </Button>
                        <Button variant="outline" size="sm" onClick={() => setIsSdkSetupOpen(true)} className="cursor-pointer text-xs h-8 flex-1 sm:flex-none">
                            <Code className="mr-2 h-3.5 w-3.5" />
                            SDK
                        </Button>
                        <Button size="sm" onClick={() => setIsCreateOpen(true)} className="cursor-pointer h-8 flex-1 sm:flex-none">
                            <Plus className="mr-2 h-3.5 w-3.5 hidden sm:inline" />
                            Create Key
                        </Button>
                    </div>
                </CardHeader>
                <CardContent>
                    <Table>
                        <TableHeader>
                            <TableRow className="border-border/40">
                                <TableHead>Name</TableHead>
                                <TableHead>Type</TableHead>
                                <TableHead className="hidden md:table-cell">Preview</TableHead>
                                <TableHead className="hidden md:table-cell">Created At</TableHead>
                                <TableHead className="hidden md:table-cell">Last Used</TableHead>
                                <TableHead className="text-right w-[80px]">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {isKeysLoading ? (
                                Array.from({ length: 3 }).map((_, i) => (
                                    <TableRow key={i} className="h-[53px] border-border/40">
                                        <TableCell><Skeleton className="h-5 w-[150px] rounded" /></TableCell>
                                        <TableCell><Skeleton className="h-5 w-16 rounded" /></TableCell>
                                        <TableCell className="hidden md:table-cell"><Skeleton className="h-5 w-32 rounded font-mono" /></TableCell>
                                        <TableCell className="hidden md:table-cell"><Skeleton className="h-5 w-24 rounded" /></TableCell>
                                        <TableCell className="hidden md:table-cell"><Skeleton className="h-5 w-24 rounded" /></TableCell>
                                        <TableCell className="text-right">
                                            <MoreHorizontal className="h-4 w-4 text-zinc-800 ml-auto animate-pulse" />
                                        </TableCell>
                                    </TableRow>
                                ))
                            ) : keys && keys.length > 0 ? (
                                keys.map((key) => (
                                    <TableRow key={key.id} className="border-border/40 hover:bg-muted/10 h-[53px] group">
                                        <TableCell className="font-medium">{key.name}</TableCell>
                                        <TableCell>
                                            <Badge variant={key.keyType === KeyType.Server ? 'default' : 'secondary'}>
                                                {key.keyType === KeyType.Server ? 'Server' : 'Client'}
                                            </Badge>
                                        </TableCell>
                                        <TableCell className="font-mono text-xs text-muted-foreground hidden md:table-cell">{key.keyPreview}</TableCell>
                                        <TableCell className="text-muted-foreground text-xs font-mono hidden md:table-cell">
                                            {formatDate(key.createdOn)}
                                        </TableCell>
                                        <TableCell className="text-muted-foreground text-xs font-mono hidden md:table-cell">
                                            {key.expireOn ? formatDate(key.expireOn) : (key.lastUsedAt ? formatDate(key.lastUsedAt) : 'Never')}
                                        </TableCell>
                                        <TableCell className="text-right">
                                            <Button
                                                variant="ghost"
                                                size="icon"
                                                className="h-8 w-8 text-muted-foreground hover:text-destructive hover:bg-muted/50 rounded-md cursor-pointer"
                                                onClick={() => handleRevokeConfirm(key.id)}
                                            >
                                                <Trash2 className="h-4 w-4" />
                                            </Button>
                                        </TableCell>
                                    </TableRow>
                                ))
                            ) : (
                                <TableRow>
                                    <TableCell colSpan={6} className="h-24 text-center text-muted-foreground">
                                        No active API keys found. Create a key to get started.
                                    </TableCell>
                                </TableRow>
                            )}
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>

            <CreateEnvironmentKeyDialog 
                open={isCreateOpen}
                onOpenChange={setIsCreateOpen}
                projectId={projectId}
                environmentId={environmentId}
            />

            <Dialog open={isRevokeConfirmOpen} onOpenChange={setIsRevokeConfirmOpen}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle className="text-destructive">Revoke API Key</DialogTitle>
                        <DialogDescription>
                            Are you sure you want to revoke this API key? Any applications currently using this key will immediately lose access.
                            This action cannot be undone.
                        </DialogDescription>
                    </DialogHeader>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setIsRevokeConfirmOpen(false)}>Cancel</Button>
                        <Button variant="destructive" onClick={executeRevoke} disabled={revokeKey.isPending}>
                            {revokeKey.isPending ? 'Revoking...' : 'Revoke Key'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
            <SdkIntegrationGuideSheet 
                open={isSdkSetupOpen} 
                onOpenChange={setIsSdkSetupOpen}
                serverKey={serverKey}
                clientKey={clientKey}
            />

            <CliIntegrationGuideSheet
                open={isCliSetupOpen}
                onOpenChange={setIsCliSetupOpen}
            />
        </div>
    );
}
