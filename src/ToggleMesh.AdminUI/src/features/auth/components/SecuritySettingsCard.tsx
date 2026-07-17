import { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Lock } from 'lucide-react';
import { Form, FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { useChangePassword, useSystemConfig } from '@/api/queries';
import { handleApiError } from '@/api/errorUtils';
import { toast } from 'sonner';

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

export function SecuritySettingsCard() {
    const changePassword = useChangePassword();
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

    return (
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
                                {changePasswordForm.formState.errors.root && (
                                    <div className="p-3 bg-destructive/10 border border-destructive/20 rounded-md">
                                        <p className="text-sm font-medium text-destructive">
                                            {changePasswordForm.formState.errors.root.message}
                                        </p>
                                    </div>
                                )}
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
    );
}
