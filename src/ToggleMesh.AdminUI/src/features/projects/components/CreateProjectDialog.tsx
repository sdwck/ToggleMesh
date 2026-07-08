import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectSeparator, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { useCreateProject, useOrganizations, useSystemConfig } from '@/api/queries';
import { useOrganizationStore } from '@/stores/useOrganizationStore';
import { handleApiError } from '@/api/errorUtils';
import { toast } from 'sonner';
import { CreateOrganizationDialog } from './CreateOrganizationDialog';

const createProjectSchema = z.object({
    name: z.string().min(1, 'Project name is required'),
    organizationId: z.string().min(1, 'Organization is required'),
});

export type CreateProjectValues = z.infer<typeof createProjectSchema>;

interface CreateProjectDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    trigger?: React.ReactNode;
}

export function CreateProjectDialog({ open, onOpenChange, trigger }: CreateProjectDialogProps) {
    const navigate = useNavigate();
    const { activeOrganizationId } = useOrganizationStore();
    const { data: organizations, isLoading: isLoadingOrgs } = useOrganizations();
    const { data: systemConfig } = useSystemConfig();
    const createProject = useCreateProject();

    const [isCreateOrgOpen, setIsCreateOrgOpen] = useState(false);

    const projectForm = useForm<CreateProjectValues>({
        resolver: zodResolver(createProjectSchema),
        defaultValues: { name: '', organizationId: '' },
    });

    useEffect(() => {
        if (open) {
            let defaultOrgId = '';
            if (organizations && organizations.length > 0) {
                if (activeOrganizationId && organizations.some(o => o.id === activeOrganizationId)) {
                    defaultOrgId = activeOrganizationId;
                } else {
                    defaultOrgId = organizations[0].id;
                }
            }
            projectForm.reset({
                name: '',
                organizationId: defaultOrgId,
            });
        }
    }, [open, activeOrganizationId, organizations, projectForm]);

    const handleCreateProject = async (values: CreateProjectValues) => {
        try {
            const newProject = await createProject.mutateAsync(values);
            onOpenChange(false);
            toast.success('Project created');
            if (newProject?.id) {
                navigate(`/projects/${newProject.id}`);
            }
        } catch (error: any) {
            handleApiError(error, projectForm.setError, 'Failed to create project');
        }
    };

    return (
        <>
            <Dialog open={open} onOpenChange={onOpenChange}>
                {trigger && <DialogTrigger asChild>{trigger}</DialogTrigger>}
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle>Create Project</DialogTitle>
                        <DialogDescription>
                            A project represents a single application or microservice.
                        </DialogDescription>
                    </DialogHeader>
                    <Form {...projectForm}>
                        <form onSubmit={projectForm.handleSubmit(handleCreateProject)} className="space-y-4 py-4 text-left">
                            <FormField
                                control={projectForm.control}
                                name="name"
                                render={({ field }) => (
                                    <FormItem>
                                        <FormLabel>Project Name</FormLabel>
                                        <FormControl>
                                            <Input
                                                placeholder="e.g., e-commerce-api"
                                                {...field}
                                                autoFocus
                                            />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />
                            <FormField
                                control={projectForm.control}
                                name="organizationId"
                                render={({ field }) => (
                                    <FormItem>
                                        <FormLabel>Organization</FormLabel>
                                        <Select value={field.value} onValueChange={(val) => {
                                            if (val === "__new__") {
                                                setIsCreateOrgOpen(true);
                                            } else {
                                                field.onChange(val);
                                            }
                                        }}>
                                            <FormControl>
                                                <SelectTrigger className="border-border/40 bg-zinc-950/50">
                                                    <SelectValue placeholder={isLoadingOrgs ? "Loading organizations..." : "Select organization"} />
                                                </SelectTrigger>
                                            </FormControl>
                                            <SelectContent className="border-border/40 bg-zinc-950/95 backdrop-blur-xl">
                                                {organizations?.map((org) => (
                                                    <SelectItem key={org.id} value={org.id}>
                                                        {org.name}
                                                    </SelectItem>
                                                ))}
                                                {systemConfig?.allowUserOrganizationCreation === true && (
                                                    <>
                                                        {organizations && organizations.length > 0 && <SelectSeparator className="bg-border/40" />}
                                                        <SelectItem value="__new__" className="text-primary focus:text-primary font-medium cursor-pointer">
                                                            + Create new organization...
                                                        </SelectItem>
                                                    </>
                                                )}
                                            </SelectContent>
                                        </Select>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />
                            {projectForm.formState.errors.root && (
                                <div className="text-sm text-destructive font-medium">
                                    {projectForm.formState.errors.root.message}
                                </div>
                            )}
                            <DialogFooter className="mt-4">
                                <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
                                <Button type="submit" disabled={createProject.isPending}>
                                    {createProject.isPending ? 'Creating...' : 'Create'}
                                </Button>
                            </DialogFooter>
                        </form>
                    </Form>
                </DialogContent>
            </Dialog>

            <CreateOrganizationDialog
                open={isCreateOrgOpen}
                onOpenChange={setIsCreateOrgOpen}
                onSuccess={(orgId) => projectForm.setValue('organizationId', orgId)}
            />
        </>
    );
}
