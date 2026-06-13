import { useState } from 'react';
import { usePersonalTokens, useCreatePersonalToken, useDeletePersonalToken } from '@/api/queries';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { KeyRound, Plus, Copy, Trash2, Key } from 'lucide-react';
import { toast } from 'sonner';
import {EmptyState} from "@/components/EmptyState.tsx";


export function AccountSettingsPage() {
    const { data: tokens, isLoading } = usePersonalTokens();
    const createToken = useCreatePersonalToken();
    const deleteToken = useDeletePersonalToken();

    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [tokenName, setTokenName] = useState('');
    const [tokenExpires, setTokenExpires] = useState<string>("30");
    const [revealedToken, setRevealedSecret] = useState<string | null>(null);

    const [tokenToDelete, setTokenToDelete] = useState<string | null>(null);

    const executeDeleteToken = async () => {
        if (!tokenToDelete) return;
        try {
            await deleteToken.mutateAsync(tokenToDelete);
            toast.success('Token deleted successfully');
            setTokenToDelete(null);
        } catch {
            toast.error('Failed to delete token');
        }
    };

    const handleCreateToken = async () => {
        if (!tokenName.trim()) {
            toast.error('Token name is required');
            return;
        }
        try {
            const expiresInDays = tokenExpires === "never" ? null : parseInt(tokenExpires, 10);
            const response = await createToken.mutateAsync({
                name: tokenName,
                expiresInDays: expiresInDays
            });
            setTokenName('');
            setRevealedSecret(response.plainToken);
            toast.success('Access Token generated');
        } catch {
            toast.error('Failed to generate token');
        }
    };

    const handleCopyToken = () => {
        if (revealedToken) {
            navigator.clipboard.writeText(revealedToken);
            toast.success('Token copied to clipboard');
        }
    };

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h2 className="text-2xl font-bold tracking-tight">Account Settings</h2>
                    <p className="text-muted-foreground">Manage your personal developer access tokens.</p>
                </div>
                <Dialog open={isDialogOpen} onOpenChange={(open) => { setIsDialogOpen(open); if (!open) setRevealedSecret(null); }}>
                    <DialogTrigger asChild>
                        <Button className="cursor-pointer">
                            <Plus className="mr-2 h-4 w-4" /> Generate Token
                        </Button>
                    </DialogTrigger>
                    <DialogContent className="border-border/40 bg-zinc-950">
                        <DialogHeader>
                            <DialogTitle>Generate Access Token</DialogTitle>
                            <DialogDescription>Access tokens authenticate CLI connections.</DialogDescription>
                        </DialogHeader>

                        {revealedToken ? (
                            <div className="space-y-4 py-4">
                                <div className="text-sm text-destructive font-semibold">
                                    Copy this token now! You will never be shown this token again.
                                </div>
                                <div className="flex items-center gap-2">
                                    <Input value={revealedToken} readOnly className="font-mono text-sm bg-muted/40" />
                                    <Button variant="outline" size="icon" onClick={handleCopyToken}>
                                        <Copy className="h-4 w-4" />
                                    </Button>
                                </div>
                            </div>
                        ) : (
                            <div className="space-y-4 py-4">
                                <div className="space-y-2">
                                    <label className="text-sm font-medium">Name</label>
                                    <Input placeholder="e.g. My Laptop CLI" value={tokenName} onChange={(e) => setTokenName(e.target.value)} />
                                </div>
                                <div className="space-y-2">
                                    <label className="text-sm font-medium">Expiration</label>
                                    <Select value={tokenExpires} onValueChange={setTokenExpires}>
                                        <SelectTrigger className="w-full bg-zinc-950/20">
                                            <SelectValue placeholder="Select expiration" />
                                        </SelectTrigger>
                                        <SelectContent className="border-border/40 bg-zinc-950">
                                            <SelectItem value="7">7 days</SelectItem>
                                            <SelectItem value="30">30 days</SelectItem>
                                            <SelectItem value="60">60 days</SelectItem>
                                            <SelectItem value="90">90 days</SelectItem>
                                            <SelectItem value="never">No expiration (Never)</SelectItem>
                                        </SelectContent>
                                    </Select>
                                </div>
                            </div>
                        )}

                        <DialogFooter>
                            {revealedToken ? (
                                <Button onClick={() => setIsDialogOpen(false)}>I have copied the token</Button>
                            ) : (
                                <>
                                    <Button variant="outline" onClick={() => setIsDialogOpen(false)}>Cancel</Button>
                                    <Button onClick={handleCreateToken} disabled={createToken.isPending}>Generate</Button>
                                </>
                            )}
                        </DialogFooter>
                    </DialogContent>
                </Dialog>
            </div>

            <Card className="border-border/40 bg-zinc-950/20">
                <CardHeader>
                    <CardTitle className="text-lg flex items-center gap-2">
                        <KeyRound className="h-5 w-5 text-muted-foreground" /> Personal Access Tokens
                    </CardTitle>
                    <CardDescription>
                        Tokens used for authenticating CLI requests. Do not share these tokens.
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Token Name</TableHead>
                                <TableHead>Preview</TableHead>
                                <TableHead>Created</TableHead>
                                <TableHead>Expires</TableHead>
                                <TableHead>Last Used</TableHead>
                                <TableHead className="text-right w-[80px]">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {isLoading ? (
                                <TableRow>
                                    <TableCell colSpan={6}><Skeleton className="h-12 w-full" /></TableCell>
                                </TableRow>
                            ) : tokens?.length === 0 ? (
                                <TableRow>
                                    <TableCell colSpan={6} className="p-0">
                                        <EmptyState
                                            icon={Key}
                                            title="No Personal Access Tokens"
                                            description="Generate a token to use the ToggleMesh CLI or API. Store it securely."
                                        />
                                    </TableCell>
                                </TableRow>
                            ) : (
                                tokens?.map((token) => (
                                    <TableRow key={token.id} className="hover:bg-muted/10 text-sm">
                                        <TableCell className="font-medium">{token.name}</TableCell>
                                        <TableCell className="font-mono text-xs text-muted-foreground">{token.preview}</TableCell>
                                        <TableCell className="text-xs text-muted-foreground font-mono">
                                            {new Date(token.createdAt).toLocaleDateString()}
                                        </TableCell>
                                        <TableCell className="text-xs text-muted-foreground font-mono">
                                            {token.expiresAt ? new Date(token.expiresAt).toLocaleDateString() : 'Never'}
                                        </TableCell>
                                        <TableCell className="text-xs text-muted-foreground font-mono">
                                            {token.lastUsedAt ? new Date(token.lastUsedAt).toLocaleString() : 'Never'}
                                        </TableCell>
                                        <TableCell className="text-right">
                                            <Button
                                                variant="ghost"
                                                size="icon"
                                                className="h-8 w-8 text-muted-foreground hover:text-destructive"
                                                onClick={() => setTokenToDelete(token.id)}
                                            >
                                                <Trash2 className="h-4 w-4" />
                                            </Button>
                                        </TableCell>
                                    </TableRow>
                                ))
                            )}
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>

            <Dialog open={!!tokenToDelete} onOpenChange={(open) => !open && setTokenToDelete(null)}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle className="text-destructive">Revoke Personal Token</DialogTitle>
                        <DialogDescription>
                            Are you sure you want to revoke this token? Any scripts or CLIs using it will immediately lose access. This action cannot be undone.
                        </DialogDescription>
                    </DialogHeader>
                    <DialogFooter className="mt-4">
                        <Button variant="outline" onClick={() => setTokenToDelete(null)}>Cancel</Button>
                        <Button variant="destructive" onClick={executeDeleteToken} disabled={deleteToken.isPending}>
                            {deleteToken.isPending ? 'Revoking...' : 'Yes, Revoke'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}