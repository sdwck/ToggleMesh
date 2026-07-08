import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { KeyRound, Plus, Copy, Trash2, Key } from 'lucide-react';
import { Form, FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { EmptyState } from "@/components/EmptyState.tsx";
import { usePersonalTokens, useCreatePersonalToken, useDeletePersonalToken } from '@/api/queries';
import { handleApiError } from '@/api/errorUtils';
import { toast } from 'sonner';

const createTokenSchema = z.object({
    name: z.string().min(1, 'Token name is required'),
    expiresIn: z.string()
});
type CreateTokenValues = z.infer<typeof createTokenSchema>;

export function PersonalAccessTokensCard() {
    const { data: tokens, isLoading } = usePersonalTokens();
    const createToken = useCreatePersonalToken();
    const deleteToken = useDeletePersonalToken();

    const createTokenForm = useForm<CreateTokenValues>({
        resolver: zodResolver(createTokenSchema),
        defaultValues: { name: '', expiresIn: '30' }
    });

    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [revealedToken, setRevealedSecret] = useState<string | null>(null);
    const [tokenToDelete, setTokenToDelete] = useState<string | null>(null);

    useEffect(() => {
        if (isDialogOpen) {
            createTokenForm.reset({ name: '', expiresIn: '30' });
        }
    }, [isDialogOpen, createTokenForm]);

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

    const handleCreateTokenSubmit = async (values: CreateTokenValues) => {
        try {
            const expiresInDays = values.expiresIn === "never" ? null : parseInt(values.expiresIn, 10);
            const response = await createToken.mutateAsync({
                name: values.name.trim(),
                expiresInDays: expiresInDays
            });
            setRevealedSecret(response.plainToken);
            toast.success('Access Token generated');
        } catch (error: any) {
            handleApiError(error, createTokenForm.setError, 'Failed to generate token');
        }
    };

    const handleCopyToken = () => {
        if (revealedToken) {
            navigator.clipboard.writeText(revealedToken);
            toast.success('Token copied to clipboard');
        }
    };

    return (
        <>
            <Card className="border-border/40 bg-zinc-950/20">
                <CardHeader className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
                    <div className="space-y-1.5">
                        <CardTitle className="text-lg flex items-center gap-2">
                            <KeyRound className="h-5 w-5 text-muted-foreground" /> Personal Access Tokens
                        </CardTitle>
                        <CardDescription>
                            Tokens used for authenticating CLI requests. Do not share these tokens.
                        </CardDescription>
                    </div>
                    <Dialog open={isDialogOpen} onOpenChange={(open) => { setIsDialogOpen(open); if (!open) setRevealedSecret(null); }}>
                        <DialogTrigger asChild>
                            <Button className="cursor-pointer">
                                <Plus className="mr-2 h-4 w-4" /> Generate Token
                            </Button>
                        </DialogTrigger>
                        <DialogContent
                            className="border-border/40 bg-zinc-950"
                        >
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
                                <Form {...createTokenForm}>
                                    <form onSubmit={createTokenForm.handleSubmit(handleCreateTokenSubmit)}>
                                        <div className="space-y-4 py-4">
                                            <div className="space-y-2">
                                                <label className="text-sm font-medium">Name</label>
                                                <FormField
                                                    control={createTokenForm.control}
                                                    name="name"
                                                    render={({ field }) => (
                                                        <FormItem>
                                                            <FormControl>
                                                                <Input {...field} placeholder="e.g. My Laptop CLI" autoFocus />
                                                            </FormControl>
                                                            <FormMessage />
                                                        </FormItem>
                                                    )}
                                                />
                                            </div>
                                            <div className="space-y-2">
                                                <label className="text-sm font-medium">Expiration</label>
                                                <FormField
                                                    control={createTokenForm.control}
                                                    name="expiresIn"
                                                    render={({ field }) => (
                                                        <FormItem>
                                                            <FormControl>
                                                                <Select value={field.value} onValueChange={field.onChange}>
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
                                                            </FormControl>
                                                            <FormMessage />
                                                        </FormItem>
                                                    )}
                                                />
                                            </div>
                                        </div>
                                        <DialogFooter>
                                            <Button type="button" variant="outline" onClick={() => setIsDialogOpen(false)}>Cancel</Button>
                                            <Button type="submit" disabled={createToken.isPending}>Generate</Button>
                                        </DialogFooter>
                                    </form>
                                </Form>
                            )}
                            {revealedToken && (
                                <DialogFooter>
                                    <Button onClick={() => setIsDialogOpen(false)}>I have copied the token</Button>
                                </DialogFooter>
                            )}
                        </DialogContent>
                    </Dialog>
                </CardHeader>
                <CardContent>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead className="whitespace-nowrap">Token Name</TableHead>
                                <TableHead className="whitespace-nowrap">Preview</TableHead>
                                <TableHead className="hidden md:table-cell whitespace-nowrap">Created</TableHead>
                                <TableHead className="hidden sm:table-cell whitespace-nowrap">Expires</TableHead>
                                <TableHead className="hidden sm:table-cell whitespace-nowrap">Last Used</TableHead>
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
                                        <TableCell className="font-medium whitespace-nowrap">{token.name}</TableCell>
                                        <TableCell className="font-mono text-xs text-muted-foreground">{token.preview}</TableCell>
                                        <TableCell className="text-xs text-muted-foreground font-mono hidden md:table-cell">
                                            {new Date(token.createdAt).toLocaleDateString()}
                                        </TableCell>
                                        <TableCell className="text-xs text-muted-foreground font-mono hidden sm:table-cell">
                                            {token.expiresAt ? new Date(token.expiresAt).toLocaleDateString() : 'Never'}
                                        </TableCell>
                                        <TableCell className="text-xs text-muted-foreground font-mono hidden sm:table-cell">
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
        </>
    );
}
