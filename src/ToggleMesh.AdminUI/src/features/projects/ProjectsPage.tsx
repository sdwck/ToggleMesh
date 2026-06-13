import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, FolderGit2, Layers } from 'lucide-react';
import { useProjects, useCreateProject } from '@/api/queries';
import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { toast } from 'sonner';

const getEnvBadgeStyle = (name: string) => {
    const lower = name.toLowerCase();
    if (lower.includes('prod') || lower.includes('prd')) {
        return "bg-rose-500/10 text-rose-400 border-rose-500/20";
    }
    if (lower.includes('dev') || lower.includes('local')) {
        return "bg-emerald-500/10 text-emerald-400 border-emerald-500/20";
    }
    if (lower.includes('stg') || lower.includes('stage') || lower.includes('test') || lower.includes('qa')) {
        return "bg-amber-500/10 text-amber-400 border-amber-500/20";
    }
    return "bg-blue-500/10 text-blue-400 border-blue-500/20";
};

export function ProjectsPage() {
    const navigate = useNavigate();
    const { data: projects, isLoading } = useProjects();
    const createProject = useCreateProject();
    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [newProjectName, setNewProjectName] = useState('');

    const handleCreate = async () => {
        if (!newProjectName.trim()) return;
        try {
            await createProject.mutateAsync(newProjectName);
            toast.success('Project created successfully');
            setNewProjectName('');
            setIsDialogOpen(false);
        } catch {
            toast.error('Failed to create project');
        }
    };

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h2 className="text-2xl font-bold tracking-tight">Projects</h2>
                    <p className="text-muted-foreground">Manage your workspace projects and environments.</p>
                </div>
                <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
                    <DialogTrigger asChild>
                        <Button className="cursor-pointer">
                            <Plus className="mr-2 h-4 w-4" />
                            New Project
                        </Button>
                    </DialogTrigger>
                    <DialogContent className="border-border/40 bg-zinc-950">
                        <DialogHeader>
                            <DialogTitle>Create Project</DialogTitle>
                            <DialogDescription>
                                A project represents a single application or microservice.
                            </DialogDescription>
                        </DialogHeader>
                        <div className="py-4">
                            <Input
                                placeholder="e.g., e-commerce-api"
                                value={newProjectName}
                                onChange={(e) => setNewProjectName(e.target.value)}
                                autoFocus
                            />
                        </div>
                        <DialogFooter>
                            <Button variant="outline" onClick={() => setIsDialogOpen(false)}>Cancel</Button>
                            <Button onClick={handleCreate} disabled={createProject.isPending || !newProjectName.trim()}>
                                {createProject.isPending ? 'Creating...' : 'Create'}
                            </Button>
                        </DialogFooter>
                    </DialogContent>
                </Dialog>
            </div>

            {isLoading ? (
                <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
                    {Array.from({ length: 3 }).map((_, i) => (
                        <Card key={i} className="border-border/40 h-44 bg-zinc-950/20">
                            <CardHeader className="pb-2">
                                <Skeleton className="h-6 w-2/3" />
                            </CardHeader>
                            <CardContent className="space-y-6 mt-2">
                                <Skeleton className="h-4 w-1/3" />
                                <div className="flex justify-between items-center border-t border-border/10 pt-4 mt-auto">
                                    <div className="flex gap-1.5">
                                        <Skeleton className="h-5 w-8 rounded" />
                                        <Skeleton className="h-5 w-8 rounded" />
                                    </div>
                                    <Skeleton className="h-8 w-14 rounded-md" />
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
                    <Button onClick={() => setIsDialogOpen(true)}>Create Project</Button>
                </Card>
            ) : (
                <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
                    {projects?.map((project) => (
                        <Card
                            key={project.id}
                            className="border-border/40 hover:border-primary/25 bg-zinc-950/20 hover:bg-zinc-950/40 transition-all cursor-pointer p-6 flex flex-col justify-between h-44 shadow-lg group"
                            onClick={() => navigate(`/projects/${project.id}/flags`)}
                        >
                            <div className="space-y-3">
                                <div className="flex items-start justify-between">
                                    <h3 className="font-semibold text-lg tracking-tight group-hover:text-primary transition-colors flex items-center gap-2">
                                        <FolderGit2 className="h-5 w-5 text-muted-foreground" />
                                        {project.name}
                                    </h3>
                                </div>
                                <div className="flex items-center gap-1.5 text-xs text-muted-foreground font-mono">
                                    <Layers className="h-3.5 w-3.5" />
                                    {project.environmentCount} environment(s)
                                </div>
                            </div>

                            <div className="flex items-center justify-between border-t border-border/10 pt-4 mt-auto">
                                <div className="flex gap-1.5 flex-wrap max-w-[180px]">
                                    {project.environments?.map((env) => (
                                        <Badge
                                            key={env.id}
                                            variant="outline"
                                            className={`text-[9px] font-semibold px-1.5 py-0.5 tracking-wide uppercase ${getEnvBadgeStyle(env.name)}`}
                                        >
                                            {env.name.length > 5 ? `${env.name.slice(0, 4)}..` : env.name}
                                        </Badge>
                                    ))}
                                </div>
                                <Button size="sm" variant="secondary" className="h-8 px-3 text-xs">View</Button>
                            </div>
                        </Card>
                    ))}
                </div>
            )}
        </div>
    );
}