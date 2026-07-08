import { useState, useMemo, useEffect } from 'react';
import {
    useOrganizationMembers,
    useInviteOrganizationMember,
    useUpdateOrganizationMember,
    useRemoveOrganizationMember,
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
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Users, UserPlus, Crown, User, Trash2, Link } from 'lucide-react';
import { toast } from 'sonner';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import { Form, FormControl, FormField, FormItem, FormMessage } from '@/components/ui/form';
import { handleApiError } from '@/api/errorUtils';

const inviteMemberSchema = z.object({
    email: z.string().email('Invalid email address'),
    role: z.string(),
});
type InviteMemberValues = z.infer<typeof inviteMemberSchema>;

export function RoleBadge({ role }: { role: OrganizationRole }) {
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

interface OrganizationMembersTabProps {
    activeOrganizationId: string;
    activeOrgName: string;
    userEmail: string;
}

export function OrganizationMembersTab({ activeOrganizationId, activeOrgName, userEmail }: OrganizationMembersTabProps) {
    const { data: members, isLoading } = useOrganizationMembers(activeOrganizationId);
    const inviteMember = useInviteOrganizationMember();
    const updateMember = useUpdateOrganizationMember();
    const removeMember = useRemoveOrganizationMember();
    
    const { data: invitations, isLoading: isLoadingInvites } = useOrganizationInvitations(activeOrganizationId);
    const revokeInvitation = useRevokeOrganizationInvitation();

    const [isInviteOpen, setIsInviteOpen] = useState(false);
    const [memberToRemove, setMemberToRemove] = useState<OrganizationMemberDto | null>(null);

    const inviteForm = useForm<InviteMemberValues>({
        resolver: zodResolver(inviteMemberSchema),
        defaultValues: { email: '', role: String(OrganizationRole.Member) },
    });

    useEffect(() => {
        if (isInviteOpen) {
            inviteForm.reset({ email: '', role: String(OrganizationRole.Member) });
        }
    }, [isInviteOpen, inviteForm]);

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

    return (
        <div className="outline-none">
            <Card className="border-border/40 bg-zinc-950/20">
                <CardHeader>
                    <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2">
                            <Users className="h-4 w-4 text-muted-foreground" />
                            <CardTitle className="text-base">Members</CardTitle>
                        </div>
                        <Button
                            size="sm"
                            className="gap-2"
                            onClick={() => setIsInviteOpen(true)}
                        >
                            <UserPlus className="h-4 w-4" />
                            Invite Member
                        </Button>
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
                                            {!isSelf ? (
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

                            {!isLoadingInvites && invitations && invitations.length > 0 && (
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
                                                    size="icon"
                                                    title="Copy invite link"
                                                    className="h-7 w-7 ml-1 text-muted-foreground hover:text-foreground"
                                                    onClick={() => {
                                                        const inviteUrl = `${window.location.origin}/invites/${invite.token}`;
                                                        navigator.clipboard.writeText(inviteUrl);
                                                        toast.success('Invite link copied to clipboard');
                                                    }}
                                                >
                                                    <Link className="h-4 w-4" />
                                                </Button>
                                                <Button
                                                    variant="ghost"
                                                    size="icon"
                                                    title="Revoke invite"
                                                    className="h-7 w-7 text-muted-foreground hover:text-destructive hover:bg-destructive/10 transition-colors ml-1"
                                                    onClick={() => handleRevokeInvite(invite.id)}
                                                    disabled={revokeInvitation.isPending}
                                                >
                                                    <Trash2 className="h-4 w-4" />
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

            <Dialog open={isInviteOpen} onOpenChange={setIsInviteOpen}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle>Invite Member</DialogTitle>
                        <DialogDescription>
                            Invite a registered user to join <strong>{activeOrgName}</strong>.
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
        </div>
    );
}
