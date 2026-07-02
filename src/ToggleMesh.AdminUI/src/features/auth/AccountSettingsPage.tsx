import { useState, useEffect } from 'react';
import {
    usePersonalTokens,
    useCreatePersonalToken,
    useDeletePersonalToken,
    useUserProfile,
    useUpdateUserProfile,
    useChangePassword,
    useSystemConfig
} from '@/api/queries';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { KeyRound, Plus, Copy, Trash2, Key, User, Lock } from 'lucide-react';
import { toast } from 'sonner';
import { EmptyState } from "@/components/EmptyState.tsx";
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import {
    Form,
    FormControl,
    FormField,
    FormItem,
    FormMessage,
} from '@/components/ui/form';
import { handleApiError } from '@/api/errorUtils';

const updateProfileSchema = z.object({
    username: z.string().min(1, 'Username is required'),
});
type UpdateProfileValues = z.infer<typeof updateProfileSchema>;

const getChangePasswordSchema = (policy: { minimumLength: number, requireDigit: boolean, requireLowercase: boolean, requireUppercase: boolean, requireNonAlphanumeric: boolean }) => {
    let passwordSchema = z.string().min(policy.minimumLength, `Password must be at least ${policy.minimumLength} characters`);

    if (policy.requireUppercase) {
        passwordSchema = passwordSchema.regex(/[A-Z]/, 'Password must contain at least one uppercase letter');
    }
    if (policy.requireLowercase) {
        passwordSchema = passwordSchema.regex(/[a-z]/, 'Password must contain at least one lowercase letter');
    }
    if (policy.requireDigit) {
        passwordSchema = passwordSchema.regex(/[0-9]/, 'Password must contain at least one number');
    }
    if (policy.requireNonAlphanumeric) {
        passwordSchema = passwordSchema.regex(/[^a-zA-Z0-9]/, 'Password must contain at least one special character');
    }

    return z.object({
        currentPassword: z.string().min(1, 'Current password is required'),
        newPassword: passwordSchema,
        confirmPassword: z.string(),
    }).refine((data) => data.newPassword === data.confirmPassword, {
        message: "Passwords do not match",
        path: ["confirmPassword"],
    });
};

type ChangePasswordValues = z.infer<ReturnType<typeof getChangePasswordSchema>>;

const createTokenSchema = z.object({
    name: z.string().min(1, 'Token name is required'),
    expiresIn: z.string()
});
type CreateTokenValues = z.infer<typeof createTokenSchema>;


export function AccountSettingsPage() {
    const { data: tokens, isLoading } = usePersonalTokens();
    const createToken = useCreatePersonalToken();
    const deleteToken = useDeletePersonalToken();

    const { data: profile } = useUserProfile();
    const updateProfile = useUpdateUserProfile();
    const changePassword = useChangePassword();

    const updateProfileForm = useForm<UpdateProfileValues>({
        resolver: zodResolver(updateProfileSchema),
        defaultValues: { username: '' }
    });

    const { data: systemConfig } = useSystemConfig();
    const defaultPolicy = { minimumLength: 8, requireDigit: true, requireLowercase: true, requireUppercase: true, requireNonAlphanumeric: true };

    const changePasswordForm = useForm<ChangePasswordValues>({
        resolver: zodResolver(getChangePasswordSchema(systemConfig?.passwordPolicy ?? defaultPolicy)),
        defaultValues: {
            currentPassword: '',
            newPassword: '',
            confirmPassword: '',
        },
    });

    const createTokenForm = useForm<CreateTokenValues>({
        resolver: zodResolver(createTokenSchema),
        defaultValues: { name: '', expiresIn: '30' }
    });

    useEffect(() => {
        if (profile) {
            updateProfileForm.reset({ username: profile.username });
        }
    }, [profile, updateProfileForm]);

    const handleUpdateProfileSubmit = async (values: UpdateProfileValues) => {
        try {
            await updateProfile.mutateAsync({ username: values.username.trim() });
            toast.success('Username updated successfully');
        } catch (error: any) {
            handleApiError(error, updateProfileForm.setError, 'Failed to update username');
        }
    };

    const [isPasswordDialogOpen, setIsPasswordDialogOpen] = useState(false);

    const handleChangePasswordSubmit = async (values: ChangePasswordValues) => {
        try {
            await changePassword.mutateAsync({
                currentPassword: values.currentPassword,
                newPassword: values.newPassword
            });
            toast.success('Password changed successfully');
            changePasswordForm.reset();
            setIsPasswordDialogOpen(false);
        } catch (error: any) {
            handleApiError(error, changePasswordForm.setError, 'Failed to change password');
        }
    };

    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [revealedToken, setRevealedSecret] = useState<string | null>(null);

    useEffect(() => {
        if (isDialogOpen) {
            createTokenForm.reset({ name: '', expiresIn: '30' });
        }
    }, [isDialogOpen, createTokenForm]);

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
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h2 className="text-2xl font-bold tracking-tight">Account Settings</h2>
                    <p className="text-muted-foreground">Manage your profile details and developer access tokens.</p>
                </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 items-start">
                <Card className="border-border/40 bg-zinc-950/20">
                    <CardHeader>
                        <CardTitle className="text-base flex items-center gap-2">
                            <User className="h-4 w-4 text-muted-foreground" /> Profile Settings
                        </CardTitle>
                        <CardDescription>
                            Manage your user profile information.
                        </CardDescription>
                    </CardHeader>
                    <CardContent>
                        <Form {...updateProfileForm}>
                            <form onSubmit={updateProfileForm.handleSubmit(handleUpdateProfileSubmit)} className="space-y-4">
                                <div className="space-y-2">
                                    <label className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70 text-muted-foreground">Email Address</label>
                                    <Input value={profile?.email || ''} readOnly disabled className="border-border/40 bg-zinc-900/40" />
                                </div>
                                <FormField
                                    control={updateProfileForm.control}
                                    name="username"
                                    render={({ field }) => (
                                        <FormItem className="space-y-2">
                                            <label className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70 text-muted-foreground">Username</label>
                                            <FormControl>
                                                <Input
                                                    {...field}
                                                    placeholder="Username"
                                                    className="border-border/40 bg-zinc-950/40"
                                                />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                                <div className="flex justify-end pt-2">
                                    <Button
                                        type="submit"
                                        disabled={updateProfile.isPending || !updateProfileForm.watch('username')?.trim() || updateProfileForm.watch('username') === profile?.username}
                                    >
                                        {updateProfile.isPending ? 'Saving...' : 'Save Changes'}
                                    </Button>
                                </div>
                            </form>
                        </Form>
                    </CardContent>
                </Card>

                <Card className="border-border/40 bg-zinc-950/20">
                    <CardHeader>
                        <CardTitle className="text-base flex items-center gap-2">
                            <Lock className="h-4 w-4 text-muted-foreground" /> Security Settings
                        </CardTitle>
                        <CardDescription>
                            Ensure your account is using a strong password.
                        </CardDescription>
                    </CardHeader>
                    <CardContent>
                        <div className="flex flex-col items-start gap-4">
                            <p className="text-sm text-muted-foreground">
                                A strong password helps prevent unauthorized access to your ToggleMesh account and projects.
                            </p>
                            <Dialog open={isPasswordDialogOpen} onOpenChange={setIsPasswordDialogOpen}>
                                <DialogTrigger asChild>
                                    <Button variant="outline">Change Password</Button>
                                </DialogTrigger>
                                <DialogContent className="sm:max-w-[425px]">
                                <DialogHeader>
                                    <DialogTitle>Change Password</DialogTitle>
                                    <DialogDescription>
                                        Enter your current password and a new password to update your credentials.
                                    </DialogDescription>
                                </DialogHeader>
                                <Form {...changePasswordForm}>
                                    <form onSubmit={changePasswordForm.handleSubmit(handleChangePasswordSubmit)} className="space-y-4">
                                        <FormField
                                            control={changePasswordForm.control}
                                            name="currentPassword"
                                            render={({ field }) => (
                                                <FormItem>
                                                    <label className="text-sm text-muted-foreground">Current Password</label>
                                                    <FormControl>
                                                        <Input
                                                            {...field}
                                                            type="password"
                                                            placeholder="Current password"
                                                            className="border-border/40 bg-zinc-950/40"
                                                        />
                                                    </FormControl>
                                                    <FormMessage />
                                                </FormItem>
                                            )}
                                        />
                                        <FormField
                                            control={changePasswordForm.control}
                                            name="newPassword"
                                            render={({ field }) => (
                                                <FormItem>
                                                    <label className="text-sm text-muted-foreground">New Password</label>
                                                    <FormControl>
                                                        <Input
                                                            {...field}
                                                            type="password"
                                                            placeholder="New password"
                                                            className="border-border/40 bg-zinc-950/40"
                                                        />
                                                    </FormControl>
                                                    <FormMessage />
                                                </FormItem>
                                            )}
                                        />
                                        <FormField
                                            control={changePasswordForm.control}
                                            name="confirmPassword"
                                            render={({ field }) => (
                                                <FormItem>
                                                    <label className="text-sm text-muted-foreground">Confirm New Password</label>
                                                    <FormControl>
                                                        <Input
                                                            {...field}
                                                            type="password"
                                                            placeholder="Confirm new password"
                                                            className="border-border/40 bg-zinc-950/40"
                                                        />
                                                    </FormControl>
                                                    <FormMessage />
                                                </FormItem>
                                            )}
                                        />
                                        <DialogFooter>
                                            <Button type="button" variant="outline" onClick={() => setIsPasswordDialogOpen(false)}>
                                                Cancel
                                            </Button>
                                            <Button
                                                type="submit"
                                                disabled={changePassword.isPending}
                                            >
                                                {changePassword.isPending ? 'Changing...' : 'Change Password'}
                                            </Button>
                                        </DialogFooter>
                                    </form>
                                </Form>
                            </DialogContent>
                        </Dialog>
                        </div>
                    </CardContent>
                </Card>
            </div>

            <Card className="border-border/40 bg-zinc-950/20">
                <CardHeader className="flex flex-row items-center justify-between">
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