import { useState, useMemo, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useOrganizationStore } from '@/stores/useOrganizationStore';
import {
    useOrganizationMembers,
    useInviteOrganizationMember,
    useOrganizations,
    useUpdateOrganizationMember,
    useRemoveOrganizationMember,
    useUpdateOrganization,
    useDeleteOrganization,
    useOrganizationInvitations,
    useRevokeOrganizationInvitation
} from '@/api/queries';
import { OrganizationRole } from '@/api/types';
import type { OrganizationMemberDto } from '@/api/types';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Building2, Users, UserPlus, Crown, User, ShieldCheck, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { jwtDecode } from 'jwt-decode';
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

const renameOrgSchema = z.object({
    name: z.string().min(1, 'Organization name is required'),
});
type RenameOrgValues = z.infer<typeof renameOrgSchema>;

const inviteMemberSchema = z.object({
    email: z.string().email('Invalid email address'),
    role: z.string(),
});
type InviteMemberValues = z.infer<typeof inviteMemberSchema>;

const deleteOrgSchema = z.object({
    confirmName: z.string().min(1, 'Confirmation is required'),
});
type DeleteOrgValues = z.infer<typeof deleteOrgSchema>;

function RoleBadge({ role }: { role: OrganizationRole }) {
    if (role === OrganizationRole.Admin) {
        return (
            <Badge className="bg-violet-500/10 text-violet-400 border-violet-500/20 gap-1 font-medium">
                <Crown className="h-3 w-3" /> Admin
            </Badge>
        );
    }
    return (
        <Badge className="bg-zinc-500/10 text-zinc-400 border-zinc-500/20 gap-1 font-medium">
            <User className="h-3 w-3" /> Member
        </Badge>
    );
}

export function OrganizationSettingsPage() {
    const { activeOrganizationId, setActiveOrganizationId } = useOrganizationStore();
    const { data: organizations } = useOrganizations();
    const activeOrg = organizations?.find(o => o.id === activeOrganizationId);
    const isAdmin = activeOrg?.role === OrganizationRole.Admin;

    const { data: members, isLoading } = useOrganizationMembers(isAdmin ? activeOrganizationId : null);
    const inviteMember = useInviteOrganizationMember();
    const updateMember = useUpdateOrganizationMember();
    const removeMember = useRemoveOrganizationMember();
    const { data: invitations, isLoading: isLoadingInvites } = useOrganizationInvitations(activeOrganizationId);
    const revokeInvitation = useRevokeOrganizationInvitation();
    const updateOrg = useUpdateOrganization();
    const deleteOrg = useDeleteOrganization();
    const navigate = useNavigate();
    const [searchParams, setSearchParams] = useSearchParams();
    let currentTab = searchParams.get('tab') || 'general';

    if (currentTab === 'members' && !isAdmin) {
        currentTab = 'general';
    }

    const [isInviteOpen, setIsInviteOpen] = useState(false);
    const [memberToRemove, setMemberToRemove] = useState<OrganizationMemberDto | null>(null);
    const [isDeleteOpen, setIsDeleteOpen] = useState(false);

    const renameForm = useForm<RenameOrgValues>({
        resolver: zodResolver(renameOrgSchema),
        defaultValues: { name: '' },
    });

    const inviteForm = useForm<InviteMemberValues>({
        resolver: zodResolver(inviteMemberSchema),
        defaultValues: { email: '', role: String(OrganizationRole.Member) },
    });

    const deleteForm = useForm<DeleteOrgValues>({
        resolver: zodResolver(deleteOrgSchema),
        defaultValues: { confirmName: '' },
    });

    useEffect(() => {
        if (activeOrg) {
            renameForm.reset({ name: activeOrg.name });
            deleteForm.reset({ confirmName: '' });
        }
    }, [activeOrg, renameForm, deleteForm]);

    useEffect(() => {
        if (isInviteOpen) {
            inviteForm.reset({ email: '', role: String(OrganizationRole.Member) });
        }
    }, [isInviteOpen, inviteForm]);

    useEffect(() => {
        if (!isDeleteOpen) {
            deleteForm.reset({ confirmName: '' });
        }
    }, [isDeleteOpen, deleteForm]);

    const userEmail = useMemo(() => {
        try {
            const token = localStorage.getItem('accessToken');
            if (!token) return '';
            const parsed: any = jwtDecode(token);
            return parsed.email || parsed['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || '';
        } catch {
            return '';
        }
    }, []);

    const sortedMembers = useMemo(() => {
        if (!members) return [];
        return [...members].sort((a, b) => {
            if (a.role !== b.role) return b.role - a.role;
            return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
        });
    }, [members]);

    const handleInviteSubmit = async (values: InviteMemberValues) => {
        if (!activeOrganizationId) return;
        try {
            await inviteMember.mutateAsync({
                organizationId: activeOrganizationId,
                email: values.email.trim(),
                role: parseInt(values.role),
            });
            toast.success(`Invitation sent to ${values.email}`);
            setIsInviteOpen(false);
        } catch (error: any) {
            handleApiError(error, inviteForm.setError, 'Failed to invite member. Make sure the email is registered.');
        }
    };

    const handleRemoveMember = async () => {
        if (!activeOrganizationId || !memberToRemove) return;
        try {
            await removeMember.mutateAsync({
                organizationId: activeOrganizationId,
                userId: memberToRemove.userId,
            });
            toast.success(`Removed ${memberToRemove.email} from organization`);
            setMemberToRemove(null);
        } catch {
            toast.error('Failed to remove member');
        }
    };

    const handleRevokeInvite = async (inviteId: string) => {
        if (!activeOrganizationId) return;
        try {
            await revokeInvitation.mutateAsync({
                organizationId: activeOrganizationId,
                inviteId
            });
            toast.success('Invitation revoked');
        } catch {
            toast.error('Failed to revoke invitation');
        }
    };

    const handleRenameSubmit = async (values: RenameOrgValues) => {
        if (!activeOrganizationId) return;
        try {
            await updateOrg.mutateAsync({
                organizationId: activeOrganizationId,
                name: values.name.trim(),
            });
            toast.success('Organization renamed successfully');
        } catch (error: any) {
            handleApiError(error, renameForm.setError, 'Failed to rename organization');
        }
    };

    const handleDeleteOrgSubmit = async (values: DeleteOrgValues) => {
        if (!activeOrganizationId || !activeOrg) return;
        if (values.confirmName !== activeOrg.name) {
            deleteForm.setError('confirmName', { type: 'manual', message: 'Name does not match' });
            return;
        }
        try {
            await deleteOrg.mutateAsync(activeOrganizationId);
            toast.success(`Organization "${activeOrg.name}" deleted successfully`);

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

    if (!activeOrganizationId) {
        return (
            <div className="flex items-center justify-center h-64 text-muted-foreground">
                <div className="text-center space-y-2">
                    <Building2 className="h-10 w-10 mx-auto opacity-30" />
                    <p>No organization selected</p>
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-8 animate-in fade-in duration-300">
            <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                    <div className="h-10 w-10 rounded-lg bg-primary/10 flex items-center justify-center">
                        <Building2 className="h-5 w-5 text-primary" />
                    </div>
                    <div>
                        <h2 className="text-2xl font-bold tracking-tight">{activeOrg?.name ?? 'Organization'}</h2>
                        <p className="text-sm text-muted-foreground flex items-center gap-1.5">
                            {isAdmin
                                ? <><ShieldCheck className="h-3.5 w-3.5 text-violet-400" /> You are an Admin</>
                                : <><User className="h-3.5 w-3.5" /> You are a Member</>
                            }
                        </p>
                    </div>
                </div>
            </div>

            <Tabs
                value={currentTab}
                onValueChange={(val) => {
                    setSearchParams(prev => {
                        prev.set('tab', val);
                        return prev;
                    });
                }}
                className="space-y-6"
            >
                <TabsList className="bg-zinc-950 border border-border/40 p-1">
                    <TabsTrigger value="general" className="text-xs">General</TabsTrigger>
                    {isAdmin && (
                        <TabsTrigger value="members" className="text-xs gap-1.5">
                            <Users className="h-3.5 w-3.5" /> Members
                            {!isLoading && (
                                <Badge variant="outline" className="px-1 py-0 text-[10px] bg-zinc-900 border-zinc-800">
                                    {members?.length ?? 0}
                                </Badge>
                            )}
                        </TabsTrigger>
                    )}
                </TabsList>

                <TabsContent value="general" className="space-y-6 outline-none">
                    {isAdmin ? (
                        <>
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
                                        <form onSubmit={renameForm.handleSubmit(handleRenameSubmit)} className="flex flex-col sm:flex-row gap-3 items-start sm:items-end">
                                            <div className="flex-1 space-y-2 w-full">
                                                <label className="text-sm text-muted-foreground">Organization Name</label>
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
                                            <Button
                                                type="submit"
                                                disabled={updateOrg.isPending || !renameForm.watch('name')?.trim() || renameForm.watch('name') === activeOrg?.name}
                                                className="w-full sm:w-auto mt-2 sm:mt-0"
                                            >
                                                {updateOrg.isPending ? 'Saving...' : 'Save Changes'}
                                            </Button>
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
                        </>
                    ) : (
                        <div className="text-center py-12 border border-border/20 rounded-lg text-muted-foreground text-sm">
                            Only organization admins can modify settings.
                        </div>
                    )}
                </TabsContent>

                {isAdmin && (
                    <TabsContent value="members" className="outline-none">
                        <Card className="border-border/40 bg-zinc-950/20">
                            <CardHeader>
                                <div className="flex items-center justify-between">
                                    <div className="flex items-center gap-2">
                                        <Users className="h-4 w-4 text-muted-foreground" />
                                        <CardTitle className="text-base">Members</CardTitle>
                                    </div>
                                    {isAdmin && (
                                        <Button
                                            size="sm"
                                            className="gap-2"
                                            onClick={() => setIsInviteOpen(true)}
                                        >
                                            <UserPlus className="h-4 w-4" />
                                            Invite Member
                                        </Button>
                                    )}
                                </div>
                                <CardDescription>People with access to this organization</CardDescription>
                            </CardHeader>
                            <CardContent>
                                {isLoading ? (
                                    <div className="space-y-3">
                                        {Array.from({ length: 3 }).map((_, i) => (
                                            <div key={i} className="flex items-center justify-between p-3 rounded-lg border border-border/20">
                                                <div className="flex items-center gap-3">
                                                    <Skeleton className="h-8 w-8 rounded-full" />
                                                    <Skeleton className="h-4 w-40" />
                                                </div>
                                                <Skeleton className="h-5 w-16 rounded-full" />
                                            </div>
                                        ))}
                                    </div>
                                ) : members?.length === 0 ? (
                                    <div className="text-center py-8 text-muted-foreground text-sm">
                                        No members found
                                    </div>
                                ) : (
                                    <div className="space-y-2">
                                        {sortedMembers.map((member) => {
                                            const isSelf = member.email === userEmail;
                                            return (
                                                <div
                                                    key={member.userId}
                                                    className="flex items-center justify-between px-3 py-2.5 rounded-lg border border-border/20 hover:border-border/40 transition-colors"
                                                >
                                                    <div className="flex items-center gap-3 min-w-0">
                                                        <Avatar className="h-8 w-8 shrink-0">
                                                            <AvatarFallback className="bg-zinc-800 text-xs font-medium">
                                                                {member.email.charAt(0).toUpperCase()}
                                                            </AvatarFallback>
                                                        </Avatar>
                                                        <span className="text-sm truncate">
                                                            {member.email}
                                                            {isSelf && (
                                                                <span className="ml-2 text-xs text-muted-foreground font-normal">
                                                                    (You)
                                                                </span>
                                                            )}
                                                        </span>
                                                    </div>
                                                    <div className="flex items-center gap-2">
                                                        {isAdmin && !isSelf ? (
                                                            <div className="flex items-center gap-2">
                                                                <Select
                                                                    value={String(member.role)}
                                                                    onValueChange={async (value) => {
                                                                        try {
                                                                            await updateMember.mutateAsync({
                                                                                organizationId: activeOrganizationId,
                                                                                userId: member.userId,
                                                                                role: parseInt(value),
                                                                            });
                                                                            toast.success(`Updated role for ${member.email}`);
                                                                        } catch {
                                                                            toast.error('Failed to update role');
                                                                        }
                                                                    }}
                                                                    disabled={updateMember.isPending}
                                                                >
                                                                    <SelectTrigger className="h-7 w-[100px] text-xs bg-zinc-900 border-border/40 hover:bg-zinc-800/50 transition-colors animate-in fade-in zoom-in-95 duration-150">
                                                                        <SelectValue />
                                                                    </SelectTrigger>
                                                                    <SelectContent className="bg-zinc-950 border-border/40">
                                                                        <SelectItem value={String(OrganizationRole.Member)}>
                                                                            <span className="flex items-center gap-1.5 text-xs">
                                                                                <User className="h-3 w-3" /> Member
                                                                            </span>
                                                                        </SelectItem>
                                                                        <SelectItem value={String(OrganizationRole.Admin)}>
                                                                            <span className="flex items-center gap-1.5 text-xs text-violet-400">
                                                                                <Crown className="h-3 w-3 text-violet-400" /> Admin
                                                                            </span>
                                                                        </SelectItem>
                                                                    </SelectContent>
                                                                </Select>
                                                                <Button
                                                                    variant="ghost"
                                                                    size="icon"
                                                                    className="h-7 w-7 text-muted-foreground hover:text-destructive hover:bg-destructive/10 rounded transition-colors"
                                                                    onClick={() => setMemberToRemove(member)}
                                                                    disabled={removeMember.isPending}
                                                                >
                                                                    <Trash2 className="h-3.5 w-3.5" />
                                                                </Button>
                                                            </div>
                                                        ) : (
                                                            <RoleBadge role={member.role} />
                                                        )}
                                                    </div>
                                                </div>
                                            );
                                        })}

                                        {isAdmin && !isLoadingInvites && invitations && invitations.length > 0 && (
                                            <>
                                                <div className="pt-4 pb-1">
                                                    <h4 className="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider flex items-center gap-1.5">
                                                        <UserPlus className="h-3 w-3" />
                                                        Pending Invitations
                                                    </h4>
                                                </div>
                                                {invitations.map((invite) => (
                                                    <div
                                                        key={invite.id}
                                                        className="flex items-center justify-between px-3 py-2.5 rounded-lg border border-border/20 hover:border-border/40 transition-colors opacity-80"
                                                    >
                                                        <div className="flex items-center gap-3 min-w-0">
                                                            <Avatar className="h-8 w-8 shrink-0 border border-dashed border-zinc-600">
                                                                <AvatarFallback className="bg-zinc-900/50 text-xs font-medium text-muted-foreground">
                                                                    {invite.email.charAt(0).toUpperCase()}
                                                                </AvatarFallback>
                                                            </Avatar>
                                                            <div className="flex flex-col">
                                                                <span className="text-sm truncate">{invite.email}</span>
                                                                <span className="text-[10px] text-muted-foreground">
                                                                    Invited on {new Date(invite.invitedAt).toLocaleDateString()}
                                                                </span>
                                                            </div>
                                                        </div>
                                                        <div className="flex items-center gap-2">
                                                            <RoleBadge role={invite.role} />
                                                            <Button
                                                                variant="ghost"
                                                                size="sm"
                                                                className="h-7 text-xs text-muted-foreground hover:text-destructive hover:bg-destructive/10 transition-colors px-2 ml-1"
                                                                onClick={() => handleRevokeInvite(invite.id)}
                                                                disabled={revokeInvitation.isPending}
                                                            >
                                                                Revoke
                                                            </Button>
                                                        </div>
                                                    </div>
                                                ))}
                                            </>
                                        )}
                                    </div>
                                )}
                            </CardContent>
                        </Card>
                    </TabsContent>
                )}
            </Tabs>

            <Dialog open={isInviteOpen} onOpenChange={setIsInviteOpen}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle>Invite Member</DialogTitle>
                        <DialogDescription>
                            Invite a registered user to join <strong>{activeOrg?.name}</strong>.
                        </DialogDescription>
                    </DialogHeader>
                    <Form {...inviteForm}>
                        <form onSubmit={inviteForm.handleSubmit(handleInviteSubmit)}>
                            <div className="space-y-4 py-2">
                                <div className="space-y-2">
                                    <label className="text-sm text-muted-foreground">Email address</label>
                                    <FormField
                                        control={inviteForm.control}
                                        name="email"
                                        render={({ field }) => (
                                            <FormItem>
                                                <FormControl>
                                                    <Input
                                                        {...field}
                                                        placeholder="user@example.com"
                                                        autoFocus
                                                    />
                                                </FormControl>
                                                <FormMessage />
                                            </FormItem>
                                        )}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <label className="text-sm text-muted-foreground">Role</label>
                                    <FormField
                                        control={inviteForm.control}
                                        name="role"
                                        render={({ field }) => (
                                            <FormItem>
                                                <Select value={field.value} onValueChange={field.onChange}>
                                                    <FormControl>
                                                        <SelectTrigger>
                                                            <SelectValue />
                                                        </SelectTrigger>
                                                    </FormControl>
                                                    <SelectContent>
                                                        <SelectItem value={String(OrganizationRole.Member)}>
                                                            <div className="flex items-center gap-2">
                                                                <User className="h-3.5 w-3.5" />
                                                                Member — access to assigned projects only
                                                            </div>
                                                        </SelectItem>
                                                        <SelectItem value={String(OrganizationRole.Admin)}>
                                                            <div className="flex items-center gap-2">
                                                                <Crown className="h-3.5 w-3.5 text-violet-400" />
                                                                Admin — full access to all projects
                                                            </div>
                                                        </SelectItem>
                                                    </SelectContent>
                                                </Select>
                                                <FormMessage />
                                            </FormItem>
                                        )}
                                    />
                                </div>
                                {inviteForm.formState.errors.root && (
                                    <div className="text-sm text-destructive font-medium">
                                        {inviteForm.formState.errors.root.message}
                                    </div>
                                )}
                            </div>
                            <DialogFooter className="mt-4">
                                <Button type="button" variant="outline" onClick={() => setIsInviteOpen(false)}>Cancel</Button>
                                <Button
                                    type="submit"
                                    disabled={inviteMember.isPending}
                                >
                                    {inviteMember.isPending ? 'Inviting...' : 'Send Invite'}
                                </Button>
                            </DialogFooter>
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>

            <Dialog open={!!memberToRemove} onOpenChange={(open) => !open && setMemberToRemove(null)}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle>Remove Member</DialogTitle>
                        <DialogDescription>
                            Are you sure you want to remove <strong>{memberToRemove?.email}</strong> from this organization?
                            This will also remove them from all projects within the organization.
                        </DialogDescription>
                    </DialogHeader>
                    <DialogFooter className="gap-2 sm:gap-0">
                        <Button variant="outline" onClick={() => setMemberToRemove(null)}>Cancel</Button>
                        <Button
                            variant="destructive"
                            onClick={handleRemoveMember}
                            disabled={removeMember.isPending}
                        >
                            {removeMember.isPending ? 'Removing...' : 'Remove Member'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            <Dialog open={isDeleteOpen} onOpenChange={setIsDeleteOpen}>
                <DialogContent className="border-destructive/20 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle className="text-red-400">Delete Organization</DialogTitle>
                        <DialogDescription className="text-muted-foreground">
                            This action is final. All project configurations, environment keys, and audit logs inside <strong>{activeOrg?.name}</strong> will be permanently deleted.
                        </DialogDescription>
                    </DialogHeader>
                    <Form {...deleteForm}>
                        <form onSubmit={deleteForm.handleSubmit(handleDeleteOrgSubmit)}>
                            <div className="space-y-4 py-2">
                                <div className="space-y-2">
                                    <p className="text-sm text-zinc-400">
                                        To confirm, type <span className="font-semibold text-foreground select-all font-mono bg-zinc-900 px-1 py-0.5 rounded">"{activeOrg?.name}"</span> below:
                                    </p>
                                    <FormField
                                        control={deleteForm.control}
                                        name="confirmName"
                                        render={({ field }) => (
                                            <FormItem>
                                                <FormControl>
                                                    <Input
                                                        {...field}
                                                        placeholder={activeOrg?.name}
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
                                    disabled={deleteOrg.isPending || deleteForm.watch('confirmName') !== activeOrg?.name}
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
