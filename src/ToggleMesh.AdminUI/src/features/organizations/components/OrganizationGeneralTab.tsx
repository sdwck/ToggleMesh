import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useUpdateOrganization, useDeleteOrganization } from '@/api/queries';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Building2, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import { Form, FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Switch } from '@/components/ui/switch';
import { handleApiError } from '@/api/errorUtils';

const renameOrgSchema = z.object({
    name: z.string().min(1, 'Organization name is required'),
    requireTwoFactor: z.boolean().optional(),
});
type RenameOrgValues = z.infer<typeof renameOrgSchema>;

const deleteOrgSchema = z.object({
    confirmName: z.string().min(1, 'Confirmation is required'),
});
type DeleteOrgValues = z.infer<typeof deleteOrgSchema>;

interface OrganizationGeneralTabProps {
    activeOrganizationId: string;
    activeOrgName: string;
    isAdmin: boolean;
    organizations: any[];
    setActiveOrganizationId: (id: string | null) => void;
}

export function OrganizationGeneralTab({ 
    activeOrganizationId, 
    activeOrgName, 
    isAdmin, 
    organizations,
    setActiveOrganizationId 
}: OrganizationGeneralTabProps) {
    const navigate = useNavigate();
    const updateOrg = useUpdateOrganization();
    const deleteOrg = useDeleteOrganization();

    const [isDeleteOpen, setIsDeleteOpen] = useState(false);

    const currentOrg = organizations?.find(o => o.id === activeOrganizationId);

    const renameForm = useForm<RenameOrgValues>({
        resolver: zodResolver(renameOrgSchema),
        defaultValues: { name: activeOrgName, requireTwoFactor: currentOrg?.requireTwoFactor ?? false },
    });

    const deleteForm = useForm<DeleteOrgValues>({
        resolver: zodResolver(deleteOrgSchema),
        defaultValues: { confirmName: '' },
    });

    useEffect(() => {
        renameForm.reset({ name: activeOrgName, requireTwoFactor: currentOrg?.requireTwoFactor ?? false });
        deleteForm.reset({ confirmName: '' });
    }, [activeOrgName, currentOrg, renameForm, deleteForm]);

    useEffect(() => {
        if (!isDeleteOpen) {
            deleteForm.reset({ confirmName: '' });
        }
    }, [isDeleteOpen, deleteForm]);

    const handleRenameSubmit = async (values: RenameOrgValues) => {
        try {
            await updateOrg.mutateAsync({
                organizationId: activeOrganizationId,
                name: values.name.trim(),
                requireTwoFactor: values.requireTwoFactor,
            });
            toast.success('Organization settings updated successfully');
        } catch (error: any) {
            handleApiError(error, renameForm.setError, 'Failed to rename organization');
        }
    };

    const handleDeleteOrgSubmit = async (values: DeleteOrgValues) => {
        if (values.confirmName !== activeOrgName) {
            deleteForm.setError('confirmName', { type: 'manual', message: 'Name does not match' });
            return;
        }
        try {
            await deleteOrg.mutateAsync(activeOrganizationId);
            toast.success(`Organization "${activeOrgName}" deleted successfully`);

            const remainingOrgs = organizations?.filter(o => o.id !== activeOrganizationId);
            if (remainingOrgs && remainingOrgs.length > 0) {
                setActiveOrganizationId(remainingOrgs[0].id);
            } else {
                setActiveOrganizationId(null);
            }

            setIsDeleteOpen(false);
            navigate('/projects');
        } catch (error: any) {
            handleApiError(error, deleteForm.setError, 'Failed to delete organization');
        }
    };

    if (!isAdmin) {
        return (
            <div className="text-center py-12 border border-border/20 rounded-lg text-muted-foreground text-sm">
                Only organization admins can modify settings.
            </div>
        );
    }

    return (
        <div className="space-y-6 outline-none">
            <Card className="border-border/40 bg-zinc-950/20">
                <CardHeader>
                    <CardTitle className="text-base flex items-center gap-2">
                        <Building2 className="h-4 w-4 text-muted-foreground" />
                        General Settings
                    </CardTitle>
                    <CardDescription>Update your organization details.</CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    <Form {...renameForm}>
                        <form onSubmit={renameForm.handleSubmit(handleRenameSubmit)} className="space-y-6">
                            {renameForm.formState.errors.root && (
                                <div className="p-3 text-sm rounded-md bg-destructive/15 text-destructive border border-destructive/20">
                                    {renameForm.formState.errors.root.message}
                                </div>
                            )}
                            <div className="space-y-4">
                                <div className="space-y-2">
                                    <label className="text-sm font-medium text-foreground">Organization Name</label>
                                    <FormField
                                        control={renameForm.control}
                                        name="name"
                                        render={({ field }) => (
                                            <FormItem>
                                                <FormControl>
                                                    <Input
                                                        {...field}
                                                        placeholder="Enter organization name"
                                                        className="border-border/40 bg-zinc-950/40"
                                                    />
                                                </FormControl>
                                                <FormMessage />
                                            </FormItem>
                                        )}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <FormField
                                        control={renameForm.control}
                                        name="requireTwoFactor"
                                        render={({ field }) => (
                                            <FormItem className="flex flex-row items-center justify-between rounded-lg border border-border/40 bg-zinc-950/40 p-4">
                                                <div className="space-y-0.5">
                                                    <label className="text-sm font-medium text-foreground">Require Two-Factor Authentication</label>
                                                    <p className="text-xs text-muted-foreground">Enforce strict mode. Users without 2FA will not be able to access any projects in this organization.</p>
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
                                </div>
                            </div>
                            <div className="flex justify-end">
                                <Button
                                    type="submit"
                                    disabled={updateOrg.isPending || (!renameForm.formState.isDirty && renameForm.watch('name') === activeOrgName && renameForm.watch('requireTwoFactor') === (currentOrg?.requireTwoFactor ?? false))}
                                    className="w-full sm:w-auto"
                                >
                                    {updateOrg.isPending ? 'Saving...' : 'Save Changes'}
                                </Button>
                            </div>
                        </form>
                    </Form>
                </CardContent>
            </Card>

            <Card className="border-destructive/20 bg-red-950/5">
                <CardHeader>
                    <CardTitle className="text-base text-red-400 flex items-center gap-2">
                        <Trash2 className="h-4 w-4 text-red-400" />
                        Danger Zone
                    </CardTitle>
                    <CardDescription className="text-red-400/70">
                        Irreversible actions for this organization.
                    </CardDescription>
                </CardHeader>
                <CardContent className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 border-t border-destructive/10 pt-6">
                    <div className="space-y-1">
                        <h4 className="text-sm font-semibold text-foreground">Delete Organization</h4>
                        <p className="text-xs text-muted-foreground max-w-lg">
                            Deleting this organization will permanently remove all associated projects, environment keys, environments, and feature flags. This action cannot be undone.
                        </p>
                    </div>
                    <Button
                        variant="destructive"
                        onClick={() => setIsDeleteOpen(true)}
                        className="w-full sm:w-auto hover:bg-destructive/90 transition-colors"
                    >
                        Delete Organization
                    </Button>
                </CardContent>
            </Card>

            <Dialog open={isDeleteOpen} onOpenChange={setIsDeleteOpen}>
                <DialogContent className="border-destructive/20 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle className="text-red-400">Delete Organization</DialogTitle>
                        <DialogDescription className="text-muted-foreground">
                            This action is final. All project configurations, environment keys, and audit logs inside <strong>{activeOrgName}</strong> will be permanently deleted.
                        </DialogDescription>
                    </DialogHeader>
                    <Form {...deleteForm}>
                        <form onSubmit={deleteForm.handleSubmit(handleDeleteOrgSubmit)}>
                            <div className="space-y-4 py-2">
                                <div className="space-y-2">
                                    <p className="text-sm text-zinc-400">
                                        To confirm, type <span className="font-semibold text-foreground select-all font-mono bg-zinc-900 px-1 py-0.5 rounded">"{activeOrgName}"</span> below:
                                    </p>
                                    <FormField
                                        control={deleteForm.control}
                                        name="confirmName"
                                        render={({ field }) => (
                                            <FormItem>
                                                <FormControl>
                                                    <Input
                                                        {...field}
                                                        placeholder={activeOrgName}
                                                        className="border-destructive/20 bg-zinc-950 focus-visible:ring-destructive font-mono"
                                                    />
                                                </FormControl>
                                                <FormMessage />
                                            </FormItem>
                                        )}
                                    />
                                </div>
                            </div>
                            <DialogFooter className="gap-2 sm:gap-0 mt-4">
                                <Button type="button" variant="outline" onClick={() => setIsDeleteOpen(false)}>Cancel</Button>
                                <Button
                                    type="submit"
                                    variant="destructive"
                                    disabled={deleteOrg.isPending || deleteForm.watch('confirmName') !== activeOrgName}
                                >
                                    {deleteOrg.isPending ? 'Deleting...' : 'Delete Permanently'}
                                </Button>
                            </DialogFooter>
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>
        </div>
    );
}
