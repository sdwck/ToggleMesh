import { useState } from 'react';
import { Plus, FolderGit2 } from 'lucide-react';
import { useProjects } from '@/api/queries';
import { useOrganizationStore } from '@/stores/useOrganizationStore';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { CreateProjectDialog } from './components/CreateProjectDialog';
import { ProjectCard } from './components/ProjectCard';

import { OrganizationSwitcher } from '@/components/layout/OrganizationSwitcher';

export function ProjectsPage() {
    const { activeOrganizationId } = useOrganizationStore();
    const { data: projects, isLoading } = useProjects(activeOrganizationId);

    const [isCreateProjectOpen, setIsCreateProjectOpen] = useState(false);

    return (
        <div className="p-4 sm:p-8 max-w-[1400px] mx-auto w-full">
            <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 mb-8">
                <div>
                    <div className="flex items-center gap-3">
                        <h1 className="text-2xl font-bold tracking-tight">Projects</h1>
                        <div className="h-6 border-l border-border/40 mx-1"></div>
                        <OrganizationSwitcher />
                    </div>
                    <p className="text-muted-foreground text-sm mt-2">Manage your workspace projects and environments.</p>
                </div>

                <CreateProjectDialog 
                    open={isCreateProjectOpen} 
                    onOpenChange={setIsCreateProjectOpen} 
                    trigger={
                        <Button className="cursor-pointer">
                            <Plus className="mr-2 h-4 w-4" />
                            New Project
                        </Button>
                    }
                />
            </div>

            {isLoading ? (
                <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
                    {[...Array(6)].map((_, i) => (
                        <Card key={i} className="border-border/40 h-full">
                            <CardContent className="p-5 h-full flex flex-col space-y-4">
                                <div className="flex items-center gap-3">
                                    <Skeleton className="h-10 w-10 rounded-xl" />
                                    <div className="space-y-1.5 flex-1">
                                        <Skeleton className="h-4 w-32 rounded" />
                                        <Skeleton className="h-3 w-16 rounded" />
                                    </div>
                                </div>
                                <div className="grid grid-cols-2 gap-3 mt-4">
                                    <Skeleton className="h-14 w-full rounded-xl" />
                                    <Skeleton className="h-14 w-full rounded-xl" />
                                </div>
                            </CardContent>
                        </Card>
                    ))}
                </div>
            ) : projects?.length === 0 ? (
                <Card className="border-border/40 border-dashed p-12 text-center">
                    <FolderGit2 className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                    <h3 className="font-semibold text-lg">No projects found</h3>
                    <p className="text-muted-foreground text-sm mb-6">Create your first project to get started.</p>
                    <Button onClick={() => setIsCreateProjectOpen(true)}>Create Project</Button>
                </Card>
            ) : (
                <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
                    {projects?.map((project) => (
                        <ProjectCard key={project.id} project={project} />
                    ))}
                </div>
            )}
        </div>
    );
}