import { useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { User, Compass } from 'lucide-react';
import { Form, FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { useUserProfile, useUpdateUserProfile } from '@/api/queries';
import { handleApiError } from '@/api/errorUtils';
import { setSkipLandingCookie, setAuthSessionCookie } from '@/utils/cookieUtils';
import { toast } from 'sonner';

const updateProfileSchema = z.object({
    username: z.string().min(1, 'Username is required'),
    skipLandingPage: z.boolean(),
});
type UpdateProfileValues = z.infer<typeof updateProfileSchema>;

export function ProfileSettingsCard() {
    const { data: profile } = useUserProfile();
    const updateProfile = useUpdateUserProfile();

    const isCloudInstance = typeof window !== 'undefined' &&
        (window.location.hostname === 'app.togglemesh.dev' || window.location.hostname === 'localhost');

    const updateProfileForm = useForm<UpdateProfileValues>({
        resolver: zodResolver(updateProfileSchema),
        defaultValues: { username: '', skipLandingPage: false }
    });

    useEffect(() => {
        if (profile) {
            updateProfileForm.reset({
                username: profile.username,
                skipLandingPage: profile.skipLandingPage ?? false
            });
            setSkipLandingCookie(profile.skipLandingPage ?? false);
            setAuthSessionCookie(true);
        }
    }, [profile, updateProfileForm]);

    const handleUpdateProfileSubmit = async (values: UpdateProfileValues) => {
        try {
            await updateProfile.mutateAsync({
                username: values.username.trim(),
                skipLandingPage: values.skipLandingPage
            });
            setSkipLandingCookie(values.skipLandingPage);
            toast.success('Profile settings updated successfully');
        } catch (error: any) {
            handleApiError(error, updateProfileForm.setError, 'Failed to update profile');
        }
    };

    return (
        <Card className="border-border/40 bg-zinc-950/20 h-full flex flex-col">
            <CardHeader>
                <CardTitle className="text-base flex items-center gap-2">
                    <User className="h-4 w-4 text-muted-foreground" /> Profile Settings
                </CardTitle>
                <CardDescription>
                    Manage your user profile information and app preferences.
                </CardDescription>
            </CardHeader>
            <CardContent className="flex-1">
                <Form {...updateProfileForm}>
                    <form onSubmit={updateProfileForm.handleSubmit(handleUpdateProfileSubmit)} className="flex flex-col h-full space-y-4">
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

                        {isCloudInstance && (
                            <FormField
                                control={updateProfileForm.control}
                                name="skipLandingPage"
                                render={({ field }) => (
                                    <FormItem className="flex items-center justify-between rounded-lg border border-border/40 p-3 bg-zinc-950/20">
                                        <div className="space-y-0.5">
                                            <label className="text-sm font-medium flex items-center gap-1.5 cursor-pointer">
                                                <Compass className="h-4 w-4 text-muted-foreground" /> Direct to App
                                            </label>
                                            <p className="text-xs text-muted-foreground">
                                                Automatically skip landing page when visiting togglemesh.dev.
                                            </p>
                                        </div>
                                        <FormControl>
                                            <Switch
                                                checked={field.value}
                                                onCheckedChange={field.onChange}
                                            />
                                        </FormControl>
                                    </FormItem>
                                )}
                            />
                        )}

                        <div className="flex justify-end mt-auto pt-4">
                            <Button
                                type="submit"
                                disabled={updateProfile.isPending}
                            >
                                {updateProfile.isPending ? 'Saving...' : 'Save Changes'}
                            </Button>
                        </div>
                    </form>
                </Form>
            </CardContent>
        </Card>
    );
}
