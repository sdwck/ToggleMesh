import { useState, useMemo } from 'react';
import { Plus, Users, Trash2, Pencil } from 'lucide-react';
import {
    useProjectMembers,
    useAddProjectMember,
    useUpdateProjectMember,
    useRemoveProjectMember
} from '@/api/queries';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger
} from '@/components/ui/dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ProjectRole } from '@/api/types';
import type { ProjectDetails, ProjectMember } from '@/api/types';
import { toast } from 'sonner';
import { jwtDecode } from "jwt-decode";
import { EmptyState } from "@/components/EmptyState.tsx";
import { Skeleton } from "@/components/ui/skeleton.tsx";
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

const addMemberSchema = z.object({
    email: z.string().email('Invalid email address'),
    role: z.string()
});
type AddMemberValues = z.infer<typeof addMemberSchema>;

const editMemberSchema = z.object({
    role: z.string(),
    environmentRoles: z.array(z.object({
        environmentId: z.string(),
        role: z.number()
    }))
});
type EditMemberValues = z.infer<typeof editMemberSchema>;

const getRoleName = (role: ProjectRole) => {
    switch (role) {
        case ProjectRole.Owner:
            return 'Owner';
        case ProjectRole.Admin:
            return 'Admin';
        case ProjectRole.Editor:
            return 'Editor';
        case ProjectRole.Viewer:
            return 'Viewer';
        case ProjectRole.None:
            return 'No Access';
        default:
            return 'Unknown';
    }
};

export function ProjectMembersTab({ project, isLoading }: { project?: ProjectDetails; isLoading: boolean }) {
    const projectId = project?.id ?? '';
    const { data: members, isLoading: isLoadingMembers } = useProjectMembers(projectId);
    const addMember = useAddProjectMember(projectId);
    const updateMember = useUpdateProjectMember(projectId);
    const removeMember = useRemoveProjectMember(projectId);

    const [isAddMemberOpen, setIsAddMemberOpen] = useState(false);
    const [memberToEdit, setMemberToEdit] = useState<ProjectMember | null>(null);
    const [memberToRemove, setMemberToRemove] = useState<ProjectMember | null>(null);

    const addForm = useForm<AddMemberValues>({
        resolver: zodResolver(addMemberSchema),
        defaultValues: { email: '', role: '3' }
    });

    const editForm = useForm<EditMemberValues>({
        resolver: zodResolver(editMemberSchema),
        defaultValues: { role: '3', environmentRoles: [] }
    });

    const canManageProject = project?.userRole === ProjectRole.Owner || project?.userRole === ProjectRole.Admin;

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
            if (a.role !== b.role) return a.role - b.role;
            return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
        });
    }, [members]);

    const handleAddMemberSubmit = async (values: AddMemberValues) => {
        try {
            await addMember.mutateAsync({ email: values.email, role: parseInt(values.role) });
            toast.success('Member added');
            addForm.reset({ email: '', role: '3' });
            setIsAddMemberOpen(false);
        } catch (error: any) {
            handleApiError(error, addForm.setError, 'Failed to add member');
        }
    };

    const handleUpdateMemberSubmit = async (values: EditMemberValues) => {
        if (!memberToEdit) return;
        try {
            await updateMember.mutateAsync({
                userId: memberToEdit.userId,
                role: parseInt(values.role),
                environmentRoles: values.environmentRoles.length > 0 ? values.environmentRoles : null
            });
            toast.success('Member updated');
            setMemberToEdit(null);
        } catch (error: any) {
            handleApiError(error, editForm.setError, 'Failed to update member');
        }
    };

    const handleEnvRoleChange = (envId: string, value: string) => {
        let newRoles = [...(editForm.getValues('environmentRoles') || [])];
        if (value === 'inherit') {
            newRoles = newRoles.filter(r => r.environmentId !== envId);
        } else {
            const existing = newRoles.find(r => r.environmentId === envId);
            if (existing) {
                existing.role = parseInt(value);
            } else {
                newRoles.push({ environmentId: envId, role: parseInt(value) });
            }
        }
        editForm.setValue('environmentRoles', newRoles, { shouldDirty: true });
    };

    const handleRemoveMember = async () => {
        if (!memberToRemove) return;
        try {
            await removeMember.mutateAsync(memberToRemove.userId);
            toast.success('Member removed');
            setMemberToRemove(null);
        } catch (err: any) {
            toast.error(err.response?.data?.errors?.[0]?.message || 'Failed to remove member');
        }
    };

    if (!canManageProject) return null;

    return (
        <div className="space-y-6">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div>
                    <h2 className="text-2xl font-bold tracking-tight">Members</h2>
                    <p className="text-muted-foreground">Manage team members for this project.</p>
                </div>
                <Dialog open={isAddMemberOpen} onOpenChange={setIsAddMemberOpen}>
                    <DialogTrigger asChild>
                        <Button>
                            <Plus className="mr-2 h-4 w-4" />
                            Add Member
                        </Button>
                    </DialogTrigger>
                    <DialogContent className="border-border/40 bg-zinc-950">
                        <DialogHeader>
                            <DialogTitle>Add Team Member</DialogTitle>
                            <DialogDescription>
                                Invite someone to collaborate on this project.
                            </DialogDescription>
                        </DialogHeader>
                        <Form {...addForm}>
                            <form onSubmit={addForm.handleSubmit(handleAddMemberSubmit)}>
                                <div className="space-y-4 py-4">
                                    <div className="space-y-2">
                                        <label className="text-sm font-medium">Email Address</label>
                                        <FormField
                                            control={addForm.control}
                                            name="email"
                                            render={({ field }) => (
                                                <FormItem>
                                                    <FormControl>
                                                        <Input {...field} placeholder="user@example.com" />
                                                    </FormControl>
                                                    <FormMessage />
                                                </FormItem>
                                            )}
                                        />
                                    </div>
                                    <div className="space-y-2">
                                        <label className="text-sm font-medium">Role</label>
                                        <FormField
                                            control={addForm.control}
                                            name="role"
                                            render={({ field }) => (
                                                <FormItem>
                                                    <FormControl>
                                                        <Select value={field.value} onValueChange={field.onChange}>
                                                            <SelectTrigger>
                                                                <SelectValue placeholder="Select a role" />
                                                            </SelectTrigger>
                                                            <SelectContent>
                                                                {project.userRole === ProjectRole.Owner &&
                                                                    <SelectItem value="0">Owner</SelectItem>}
                                                                <SelectItem value="1">Admin</SelectItem>
                                                                <SelectItem value="2">Editor</SelectItem>
                                                                <SelectItem value="3">Viewer</SelectItem>
                                                            </SelectContent>
                                                        </Select>
                                                    </FormControl>
                                                    <FormMessage />
                                                </FormItem>
                                            )}
                                        />
                                    </div>
                                </div>
                                {addForm.formState.errors.root && (
                                    <div className="text-sm text-destructive font-medium px-1">
                                        {addForm.formState.errors.root.message}
                                    </div>
                                )}
                                <DialogFooter className="mt-4">
                                    <Button type="button" variant="outline" onClick={() => setIsAddMemberOpen(false)}>Cancel</Button>
                                    <Button type="submit" disabled={addMember.isPending}>
                                        {addMember.isPending ? 'Adding...' : 'Add Member'}
                                    </Button>
                                </DialogFooter>
                            </form>
                        </Form>
                    </DialogContent>
                </Dialog>
            </div>

            <Card className="border-border/40">
                <CardHeader>
                    <CardTitle className="text-lg flex items-center gap-2">
                        <Users className="h-5 w-5 text-muted-foreground" />
                        Team Members
                    </CardTitle>
                    <CardDescription>Users who have access to this project.</CardDescription>
                </CardHeader>
                <CardContent>
                    {isLoadingMembers || isLoading ? (
                        <div className="rounded-md border border-border/40 overflow-hidden">
                            <Table>
                                <TableHeader>
                                    <TableRow className="hover:bg-transparent">
                                        <TableHead>User</TableHead>
                                        <TableHead>Role</TableHead>
                                        <TableHead className="text-right">Actions</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {Array.from({ length: 2 }).map((_, i) => (
                                        <TableRow key={i} className="h-[53px] border-border/40">
                                            <TableCell><Skeleton className="h-5 w-[200px] rounded" /></TableCell>
                                            <TableCell><Skeleton className="h-5 w-16 rounded" /></TableCell>
                                            <TableCell><Skeleton className="h-8 w-12 rounded ml-auto" /></TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        </div>
                    ) : (
                        <div className="rounded-md border border-border/40 overflow-hidden">
                            <Table>
                                <TableHeader>
                                    <TableRow className="hover:bg-transparent">
                                        <TableHead>User</TableHead>
                                        <TableHead>Role</TableHead>
                                        <TableHead className="text-right">Actions</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {sortedMembers.map((member) => {
                                        const isSelf = member.email === userEmail;
                                        return (
                                            <TableRow key={member.id} className="border-border/40 hover:bg-muted/30 h-[53px]">
                                                <TableCell className="py-2">
                                                    <div className="flex items-center gap-3">
                                                        <div className="h-8 w-8 rounded-full bg-primary/10 flex items-center justify-center border border-primary/5">
                                                            <Users className="h-4 w-4 text-primary" />
                                                        </div>
                                                        <div className="flex flex-col">
                                                            <span className="font-medium text-sm">
                                                                {member.email}
                                                                {isSelf && <span className="ml-2 text-xs text-muted-foreground font-normal">(You)</span>}
                                                            </span>
                                                            <span className="text-[10px] text-muted-foreground/50 font-mono mt-0.5">{member.userId}</span>
                                                        </div>
                                                    </div>
                                                </TableCell>
                                                <TableCell className="py-2">
                                                    <div className="flex items-center gap-2">
                                                        <span
                                                            title={member.isOrganizationAdmin ? "Inherited from Organization" : undefined}
                                                            className={`inline-flex items-center rounded bg-secondary/50 px-2 py-0.5 text-xs font-medium text-secondary-foreground border border-border/10 ${member.isOrganizationAdmin ? 'cursor-help' : ''}`}
                                                        >
                                                            {getRoleName(member.role)}
                                                        </span>
                                                    </div>
                                                </TableCell>
                                                <TableCell className="text-right py-2">
                                                    {!isSelf && !member.isOrganizationAdmin && !(project.userRole === ProjectRole.Admin && member.role === ProjectRole.Owner) && (
                                                        <div className="flex items-center justify-end gap-2 pr-2">
                                                            <Button
                                                                variant="ghost"
                                                                size="icon"
                                                                className="h-8 w-8 text-muted-foreground hover:text-foreground hover:bg-muted/50 rounded-md cursor-pointer"
                                                                onClick={() => {
                                                                    setMemberToEdit(member);
                                                                    editForm.reset({
                                                                        role: member.role.toString(),
                                                                        environmentRoles: member.environmentRoles || []
                                                                    });
                                                                }}
                                                                title="Edit Role"
                                                            >
                                                                <Pencil className="h-4 w-4" />
                                                            </Button>
                                                            <Button
                                                                variant="ghost"
                                                                size="icon"
                                                                className="h-8 w-8 text-muted-foreground hover:text-destructive hover:bg-muted/50 rounded-md cursor-pointer"
                                                                onClick={() => setMemberToRemove(member)}
                                                            >
                                                                <Trash2 className="h-4 w-4" />
                                                            </Button>
                                                        </div>
                                                    )}
                                                </TableCell>
                                            </TableRow>
                                        );
                                    })}

                                    {members?.length === 0 && (
                                        <TableRow className="hover:bg-transparent">
                                            <TableCell colSpan={3} className="p-0 border-none">
                                                <EmptyState
                                                    icon={Users}
                                                    title="No Team Members"
                                                    description="Invite colleagues to collaborate on this project."
                                                />
                                            </TableCell>
                                        </TableRow>
                                    )}
                                </TableBody>
                            </Table>
                        </div>
                    )}
                </CardContent>
            </Card>

            <Dialog open={!!memberToEdit} onOpenChange={(open) => !open && setMemberToEdit(null)}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Edit Member Access</DialogTitle>
                        <DialogDescription>
                            Update permissions for {memberToEdit?.email}.
                        </DialogDescription>
                    </DialogHeader>
                    <Form {...editForm}>
                        <form onSubmit={editForm.handleSubmit(handleUpdateMemberSubmit)}>
                            <div className="py-4 space-y-4">
                                <div className="space-y-2">
                                    <label className="text-sm font-medium">Role</label>
                                    <FormField
                                        control={editForm.control}
                                        name="role"
                                        render={({ field }) => (
                                            <FormItem>
                                                <FormControl>
                                                    <Select value={field.value} onValueChange={field.onChange}>
                                                        <SelectTrigger>
                                                            <SelectValue placeholder="Select a role" />
                                                        </SelectTrigger>
                                                        <SelectContent>
                                                            {project?.userRole === ProjectRole.Owner && <SelectItem value="0">Owner</SelectItem>}
                                                            <SelectItem value="1">Admin</SelectItem>
                                                            <SelectItem value="2">Editor</SelectItem>
                                                            <SelectItem value="3">Viewer</SelectItem>
                                                        </SelectContent>
                                                    </Select>
                                                </FormControl>
                                                <FormMessage />
                                            </FormItem>
                                        )}
                                    />
                                </div>

                                {(editForm.watch('role') === '2' || editForm.watch('role') === '3') && (
                                    <div className="space-y-2 pt-2">
                                        <label className="text-sm font-medium">Environment Overrides</label>
                                        <div
                                            className="grid gap-3 border border-border/40 p-4 rounded-md max-h-[220px] overflow-auto">
                                            {project?.environments.map(env => {
                                                const envRoles = editForm.watch('environmentRoles') || [];
                                                const override = envRoles.find(e => e.environmentId === env.id);
                                                const value = override ? override.role.toString() : 'inherit';
                                                return (
                                                    <div key={env.id} className="flex items-center justify-between">
                                                        <span className="text-sm font-medium">{env.name}</span>
                                                        <Select value={value}
                                                            onValueChange={(val) => handleEnvRoleChange(env.id, val)}>
                                                            <SelectTrigger className="w-[160px] h-8 text-xs">
                                                                <SelectValue />
                                                            </SelectTrigger>
                                                            <SelectContent>
                                                                <SelectItem value="inherit">Inherit
                                                                    ({getRoleName(parseInt(editForm.watch('role')) as ProjectRole)})</SelectItem>
                                                                <SelectItem value="1">Admin</SelectItem>
                                                                <SelectItem value="2">Editor</SelectItem>
                                                                <SelectItem value="3">Viewer</SelectItem>
                                                                <SelectItem value="4">No Access</SelectItem>
                                                            </SelectContent>
                                                        </Select>
                                                    </div>
                                                );
                                            })}
                                        </div>
                                    </div>
                                )}
                            </div>
                            {editForm.formState.errors.root && (
                                <div className="text-sm text-destructive font-medium px-1">
                                    {editForm.formState.errors.root.message}
                                </div>
                            )}
                            <DialogFooter className="mt-4">
                                <Button type="button" variant="outline" onClick={() => setMemberToEdit(null)}>Cancel</Button>
                                <Button type="submit" disabled={updateMember.isPending}>
                                    {updateMember.isPending ? 'Saving...' : 'Save Changes'}
                                </Button>
                            </DialogFooter>
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>

            <Dialog open={!!memberToRemove} onOpenChange={(open) => !open && setMemberToRemove(null)}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Remove Member</DialogTitle>
                        <DialogDescription>
                            Are you sure you want to remove <strong>{memberToRemove?.email}</strong> from this project?
                            They will lose access to all environments and feature flags.
                        </DialogDescription>
                    </DialogHeader>
                    <DialogFooter className="mt-4">
                        <Button variant="outline" onClick={() => setMemberToRemove(null)}>Cancel</Button>
                        <Button variant="destructive" onClick={handleRemoveMember} disabled={removeMember.isPending}>
                            {removeMember.isPending ? 'Removing...' : 'Remove Member'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
