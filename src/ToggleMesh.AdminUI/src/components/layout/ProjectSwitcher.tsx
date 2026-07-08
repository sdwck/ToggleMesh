import { useState } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { Check, ChevronsUpDown, FolderGit2 } from 'lucide-react';
import { useProjects } from '@/api/queries';
import { useOrganizationStore } from '@/stores/useOrganizationStore';
import { Button } from '@/components/ui/button';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';

export function ProjectSwitcher({ variant = 'header' }: { variant?: 'header' | 'sidebar' }) {
    const { projectId } = useParams();
    const navigate = useNavigate();
    const location = useLocation();
    const { activeOrganizationId } = useOrganizationStore();
    const { data: projects, isLoading } = useProjects(activeOrganizationId);
    const [dropdownOpen, setDropdownOpen] = useState(false);

    if (isLoading) {
        return <Skeleton className="h-9 w-[150px]" />;
    }

    const activeProject = projects?.find(p => p.id === projectId);

    const handleSelect = (newProjectId: string) => {
        const currentPath = location.pathname;
        let targetSection = '';
        if (currentPath.includes('/environments')) targetSection = '/environments';
        else if (currentPath.includes('/members')) targetSection = '/members';
        else if (currentPath.includes('/audit')) targetSection = '/audit';
        else if (currentPath.includes('/settings')) targetSection = '/settings';
        else if (currentPath.includes('/playground')) targetSection = '/playground';
        else if (currentPath.includes('/flags')) targetSection = '/flags';

        navigate(`/projects/${newProjectId}${targetSection}`);
        setDropdownOpen(false);
    };

    const buttonClasses = variant === 'sidebar'
        ? "w-full flex items-center justify-between h-11 px-3 border border-border/40 bg-zinc-950/50 hover:bg-zinc-900 rounded-md text-sm font-medium transition-colors shadow-sm"
        : "h-9 px-2 text-lg font-bold tracking-tight hover:bg-zinc-900/50 flex items-center gap-1.5 focus-visible:ring-0";

    return (
        <DropdownMenu open={dropdownOpen} onOpenChange={setDropdownOpen}>
            <DropdownMenuTrigger asChild>
                <Button variant="ghost" role="combobox" className={buttonClasses}>
                    <div className="flex items-center gap-2 truncate">
                        {variant === 'sidebar' && <FolderGit2 className="h-4 w-4 text-blue-500/70 shrink-0" />}
                        <span className="truncate">{activeProject?.name || 'Select Project...'}</span>
                    </div>
                    <ChevronsUpDown className="h-4 w-4 shrink-0 opacity-50" />
                </Button>
            </DropdownMenuTrigger>
             <DropdownMenuContent align="start" className="w-[220px] border-border/40 bg-zinc-950/95 backdrop-blur-xl z-[100]">
                <DropdownMenuLabel className="text-xs text-muted-foreground font-semibold">Projects</DropdownMenuLabel>
                <div style={projects && projects.length > 10 ? { maxHeight: '320px', overflowY: 'auto' } : undefined}>
                    {projects?.map((project) => (
                        <DropdownMenuItem
                            key={project.id}
                            onSelect={() => handleSelect(project.id)}
                            className="flex items-center justify-between cursor-pointer focus:bg-primary/10"
                        >
                            <span className="truncate">{project.name}</span>
                            {projectId === project.id && (
                                <Check className="h-4 w-4 shrink-0 text-primary" />
                            )}
                        </DropdownMenuItem>
                    ))}
                </div>
            </DropdownMenuContent>
        </DropdownMenu>
    );
}
