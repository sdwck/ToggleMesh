import {useParams, useSearchParams} from 'react-router-dom';
import {useCreateFeatureFlag, useProjectDetails, useProjectTags} from '@/api/queries';
import {ProjectFlagsTab} from './ProjectFlagsTab';
import {Plus, Search, Tag} from "lucide-react";
import {Input} from "@/components/ui/input.tsx";
import {useState, useEffect} from "react";
import {useQueryClient} from '@tanstack/react-query';
import {toast} from "sonner";
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger
} from "@/components/ui/dialog";
import {Button} from "@/components/ui/button.tsx";
import {ProjectRole, type Project} from "@/api/types.ts";
import {Skeleton} from "@/components/ui/skeleton";
import {Card} from "@/components/ui/card";
import {Table, TableBody, TableHead, TableHeader, TableRow} from "@/components/ui/table";
import {TableSkeleton} from "@/components/TableSkeleton";
import {Badge} from '@/components/ui/badge';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select';

export function ProjectFlagsPage() {
    const {projectId} = useParams<{ projectId: string }>();
    const queryClient = useQueryClient();

    const {data: project, isLoading} = useProjectDetails(projectId!);
    const {data: projectTags} = useProjectTags(projectId!);
    const createFlag = useCreateFeatureFlag(projectId!);

    const [newFlagKey, setNewFlagKey] = useState('');
    const [isCreateOpen, setIsCreateOpen] = useState(false);
    const [isCreating, setIsCreating] = useState(false);

    const [searchParams] = useSearchParams();
    const initialTag = searchParams.get('tag');

    const [inputValue, setInputValue] = useState('');
    const [search, setSearch] = useState('');
    const [selectedTags, setSelectedTags] = useState<string[]>(initialTag ? [initialTag] : []);
    const [sortBy, setSortBy] = useState<string>('updated-desc');

    useEffect(() => {
        const tag = searchParams.get('tag');
        if (tag) {
            setSelectedTags([tag]);
        }
    }, [searchParams]);

    useEffect(() => {
        const handler = setTimeout(() => {
            setSearch(inputValue);
        }, 300);

        return () => clearTimeout(handler);
    }, [inputValue]);

    const cachedProjects = queryClient.getQueryData<Project[]>(['projects']);
    const cachedProject = cachedProjects?.find(p => p.id === projectId);

    const activeProject = project || cachedProject;

    const canEditFlags = activeProject
        ? (activeProject.userRole === ProjectRole.Owner || activeProject.userRole === ProjectRole.Admin || activeProject.userRole === ProjectRole.Editor)
        : false;

    const handleCreateFlag = async () => {
        if (!newFlagKey.trim()) return;

        setIsCreating(true);

        try {
            await createFlag.mutateAsync(newFlagKey.trim());
            toast.success(`Successfully created feature flag`);
            setNewFlagKey('');
            setIsCreateOpen(false);
        } catch (e) {
            console.error(`Failed to create flag`, e);
            toast.error('Failed to create flag. It might already exist.');
        } finally {
            setIsCreating(false);
        }
    };

    const handleTagClick = (tag: string) => {
        if (selectedTags.includes(tag)) {
            setSelectedTags(selectedTags.filter(t => t !== tag));
        } else {
            setSelectedTags([...selectedTags, tag]);
        }
    };

    return (
        <div className="space-y-6">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div className="space-y-1">
                    <h2 className="text-2xl font-bold tracking-tight h-8 flex items-center">
                        {isLoading && !cachedProject ? (
                            <Skeleton className="h-7 w-48"/>
                        ) : (
                            project?.name ?? cachedProject?.name
                        )}
                    </h2>
                    <p className="text-muted-foreground">Manage feature flags for this project.</p>
                </div>

                {isLoading && !cachedProject ? (
                    <Skeleton className="h-10 w-28"/>
                ) : canEditFlags ? (
                    <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
                        <DialogTrigger asChild>
                            <Button className="cursor-pointer">
                                <Plus className="mr-2 h-4 w-4"/>
                                New Flag
                            </Button>
                        </DialogTrigger>
                        <DialogContent className="border-border/40 bg-zinc-950">
                            <DialogHeader>
                                <DialogTitle>Create Feature Flag</DialogTitle>
                                <DialogDescription>
                                    Enter a unique key for the new feature flag.
                                </DialogDescription>
                            </DialogHeader>
                            <div className="py-4">
                                <Input
                                    placeholder="e.g. new-billing-ui"
                                    value={newFlagKey}
                                    onChange={(e) => setNewFlagKey(e.target.value)}
                                    autoFocus
                                    className="font-mono"
                                />
                            </div>
                            <DialogFooter>
                                <Button variant="outline" onClick={() => setIsCreateOpen(false)}>Cancel</Button>
                                <Button onClick={handleCreateFlag} disabled={isCreating || !newFlagKey.trim()}>
                                    {isCreating ? 'Creating...' : 'Create Flag'}
                                </Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>
                ) : null}
            </div>

            <div className="space-y-3">
                <div className="flex flex-col sm:flex-row sm:items-center gap-4">
                    <div className="flex items-center max-w-sm relative flex-1">
                        <Search className="h-4 w-4 text-muted-foreground absolute left-3"/>
                        <Input
                            placeholder="Search flags by key or name..."
                            value={inputValue}
                            onChange={(e) => setInputValue(e.target.value)}
                            className="pl-9 h-10 border-border/40 bg-zinc-950/20"
                        />
                    </div>

                    <Select value={sortBy} onValueChange={setSortBy}>
                        <SelectTrigger className="w-[180px] bg-zinc-950/20 border-border/40">
                            <SelectValue placeholder="Sort by"/>
                        </SelectTrigger>
                        <SelectContent className="border-border/40 bg-zinc-950">
                            <SelectItem value="updated-desc">Recently Updated</SelectItem>
                            <SelectItem value="updated-asc">Oldest Updated</SelectItem>
                            <SelectItem value="date-desc">Newest Created</SelectItem>
                            <SelectItem value="date-asc">Oldest Created</SelectItem>
                            <SelectItem value="key-asc">Key (A-Z)</SelectItem>
                            <SelectItem value="key-desc">Key (Z-A)</SelectItem>
                        </SelectContent>
                    </Select>
                </div>

                {isLoading && (<div className="flex items-center gap-2 flex-wrap text-xs pt-1">
                    <span className="text-muted-foreground flex items-center gap-1"><Tag className="h-3 w-3"/> Filter by tags:</span>
                    <Skeleton className="h-[1.5rem] w-16 rounded-md" />
                </div>)}
                {!isLoading && projectTags && projectTags.length > 0 && (
                    <div className="flex items-center gap-2 flex-wrap text-xs pt-1">
                        <span className="text-muted-foreground flex items-center gap-1"><Tag className="h-3 w-3"/> Filter by tags:</span>
                        {projectTags.map(tag => {
                            const isSelected = selectedTags.includes(tag);
                            return (
                                <Badge
                                    key={tag}
                                    variant="outline"
                                    onClick={() => handleTagClick(tag)}
                                    className={`cursor-pointer uppercase text-[9px] font-semibold px-2 py-0.5 tracking-wide transition-all ${
                                        isSelected
                                            ? "bg-primary/10 text-primary border-primary/30"
                                            : "bg-zinc-900/40 text-muted-foreground border-zinc-800 hover:text-foreground hover:border-zinc-700"
                                    }`}
                                >
                                    {tag}
                                </Badge>
                            );
                        })}
                    </div>
                )}
            </div>

            {activeProject ? (
                <ProjectFlagsTab
                    project={activeProject as any}
                    search={search}
                    tags={selectedTags}
                    sortBy={sortBy}
                    isLoadingProject={isLoading && !project}
                />
            ) : (
                <Card className="border-border/40 overflow-hidden bg-zinc-950/20">
                    <Table wrapperClassName="max-h-[600px] overflow-auto">
                        <TableHeader>
                            <TableRow className="hover:bg-transparent border-border/40 shadow-sm h-10">
                                <TableHead className="w-[280px]">Flag Key</TableHead>
                                <TableHead className="w-[120px] text-center">Client Side</TableHead>
                                <TableHead className="text-center">Development</TableHead>
                                <TableHead className="text-center">Production</TableHead>
                                <TableHead className="text-right w-[80px]">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            <TableSkeleton columnsCount={5} rowsCount={3}/>
                        </TableBody>
                    </Table>
                </Card>
            )}
        </div>
    );
}