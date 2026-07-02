import { useParams, useSearchParams } from 'react-router-dom';
import { useCreateFeatureFlag, useProjectDetails, useProjectTags } from '@/api/queries';
import { ProjectFlagsTab } from './ProjectFlagsTab';
import { Plus, Search, Tag, Check, X } from "lucide-react";
import { Input } from "@/components/ui/input.tsx";
import { useState, useEffect, useRef } from "react";
import { useQueryClient } from '@tanstack/react-query';
import { toast } from "sonner";
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button.tsx";
import { ProjectRole, type Project } from "@/api/types.ts";
import { Skeleton } from "@/components/ui/skeleton";
import { Card } from "@/components/ui/card";
import { Table, TableBody, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { TableSkeleton } from "@/components/TableSkeleton";
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
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

const createFlagSchema = z.object({
    key: z.string().min(1, 'Flag key is required')
        .regex(/^[a-zA-Z0-9_-]+$/, 'Only letters, numbers, hyphens, and underscores are allowed')
});
type CreateFlagValues = z.infer<typeof createFlagSchema>;

export function ProjectFlagsPage() {
    const { projectId } = useParams<{ projectId: string }>();
    const queryClient = useQueryClient();

    const { data: project, isLoading } = useProjectDetails(projectId!);
    const { data: projectTags, isLoading: isLoadingTags } = useProjectTags(projectId!);
    const createFlag = useCreateFeatureFlag(projectId!);

    const [isCreateOpen, setIsCreateOpen] = useState(false);

    const createForm = useForm<CreateFlagValues>({
        resolver: zodResolver(createFlagSchema),
        defaultValues: { key: '' }
    });

    const [isTagsOpen, setIsTagsOpen] = useState(false);
    const [tagSearch, setTagSearch] = useState('');
    const [visibleTagsCount, setVisibleTagsCount] = useState(25);
    const tagsRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        function handleClickOutside(event: MouseEvent) {
            if (tagsRef.current && !tagsRef.current.contains(event.target as Node)) {
                setIsTagsOpen(false);
            }
        }
        if (isTagsOpen) {
            document.addEventListener('mousedown', handleClickOutside);
        }
        return () => {
            document.removeEventListener('mousedown', handleClickOutside);
        };
    }, [isTagsOpen]);

    useEffect(() => {
        setVisibleTagsCount(25);
    }, [tagSearch, isTagsOpen]);

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

    const handleCreateFlagSubmit = async (values: CreateFlagValues) => {
        try {
            await createFlag.mutateAsync(values.key.trim());
            toast.success(`Successfully created feature flag`);
            createForm.reset({ key: '' });
            setIsCreateOpen(false);
        } catch (error: any) {
            handleApiError(error, createForm.setError, 'Failed to create flag. It might already exist.');
        }
    };

    const filteredTags = projectTags
        ? projectTags.filter(tag => tag.toLowerCase().includes(tagSearch.toLowerCase()))
        : [];

    const visibleTags = filteredTags.slice(0, visibleTagsCount);

    const handleTagsScroll = (e: React.UIEvent<HTMLDivElement>) => {
        const target = e.currentTarget;
        if (target.scrollHeight - target.scrollTop <= target.clientHeight + 20) {
            if (visibleTagsCount < filteredTags.length) {
                setVisibleTagsCount(prev => Math.min(prev + 25, filteredTags.length));
            }
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
        <div className="relative">
            <div className="sticky top-0 z-30 bg-background/95 backdrop-blur pb-4 mb-4 space-y-3 border-b border-border/10">
                <div className="flex flex-col sm:flex-row sm:items-center gap-4">
                    <div className="flex items-center flex-1 border border-border/40 bg-zinc-950/20 rounded-md focus-within:ring-1 focus-within:ring-ring focus-within:border-primary/50 transition-all h-10">
                        <div className="flex items-center relative flex-1 h-full">
                            <Search className="h-4 w-4 text-muted-foreground absolute left-3" />
                            <Input
                                placeholder="Search flags by key or name..."
                                value={inputValue}
                                onChange={(e) => setInputValue(e.target.value)}
                                className="pl-9 h-full border-none bg-transparent shadow-none focus-visible:ring-0 focus-visible:ring-offset-0"
                            />
                        </div>

                        <div className="w-[1px] h-5 bg-border/40 shrink-0" />

                        <div className="relative h-full shrink-0" ref={tagsRef}>
                            <button
                                onClick={() => setIsTagsOpen(!isTagsOpen)}
                                className={`h-full cursor-pointer px-3 text-sm text-muted-foreground hover:text-foreground transition-colors flex items-center gap-2 border-none bg-transparent select-none outline-none focus:outline-none ${selectedTags.length > 0 ? "text-primary hover:text-primary/80" : ""
                                    }`}
                            >
                                <Tag className="h-4 w-4" />
                                <span>Tags</span>
                                {selectedTags.length > 0 && (
                                    <Badge variant="secondary" className="ml-1 px-1.5 py-0.5 text-[10px] font-semibold bg-primary/20 text-primary border-none animate-in zoom-in-95 duration-100">
                                        {selectedTags.length}
                                    </Badge>
                                )}
                            </button>

                            {isTagsOpen && (
                                <div className="absolute right-0 mt-2 w-64 rounded-md border border-border/40 bg-zinc-950 p-2 shadow-lg z-50 animate-in fade-in-0 zoom-in-95 duration-100 origin-top-right">
                                    <div className="flex items-center gap-2 border-b border-border/10 pb-2 mb-2 px-1">
                                        <Search className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
                                        <input
                                            type="text"
                                            placeholder="Search tags..."
                                            value={tagSearch}
                                            onChange={(e) => setTagSearch(e.target.value)}
                                            className="w-full bg-transparent text-sm border-none outline-none focus:ring-0 placeholder:text-muted-foreground/60 text-foreground"
                                        />
                                        {tagSearch && (
                                            <button onClick={() => setTagSearch('')} className="text-muted-foreground hover:text-foreground">
                                                <X className="h-3.5 w-3.5" />
                                            </button>
                                        )}
                                    </div>

                                    <div
                                        className="max-h-60 overflow-y-auto space-y-0.5 scrollbar-thin"
                                        onScroll={handleTagsScroll}
                                    >
                                        {isLoadingTags && (
                                            <div className="text-xs text-muted-foreground text-center py-4">
                                                Loading tags...
                                            </div>
                                        )}

                                        {!isLoadingTags && (!projectTags || projectTags.length === 0) && (
                                            <div className="text-xs text-muted-foreground text-center py-4 px-2">
                                                No tags in this project
                                            </div>
                                        )}

                                        {!isLoadingTags && projectTags && projectTags.length > 0 && filteredTags.length === 0 && (
                                            <div className="text-xs text-muted-foreground text-center py-4">
                                                No tags found
                                            </div>
                                        )}

                                        {!isLoadingTags && visibleTags.map(tag => {
                                            const isSelected = selectedTags.includes(tag);
                                            return (
                                                <div
                                                    key={tag}
                                                    onClick={() => handleTagClick(tag)}
                                                    className={`flex items-center gap-2 px-2 py-1.5 rounded-sm text-sm cursor-pointer select-none transition-colors ${isSelected
                                                        ? "bg-primary/10 text-primary font-medium"
                                                        : "text-muted-foreground hover:bg-zinc-900 hover:text-foreground"
                                                        }`}
                                                >
                                                    <div className={`w-4 h-4 rounded border flex items-center justify-center border-border/60 ${isSelected ? "border-primary bg-primary text-primary-foreground" : ""}`}>
                                                        {isSelected && <Check className="h-3 w-3 stroke-[3]" />}
                                                    </div>
                                                    <span className="uppercase text-[10px] tracking-wide font-semibold truncate">{tag}</span>
                                                </div>
                                            );
                                        })}
                                    </div>

                                    {selectedTags.length > 0 && (
                                        <div className="border-t border-border/10 pt-2 mt-2 flex justify-between items-center px-1">
                                            <span className="text-[10px] text-muted-foreground">{selectedTags.length} selected</span>
                                            <button
                                                onClick={() => setSelectedTags([])}
                                                className="text-[10px] text-primary hover:underline cursor-pointer font-medium"
                                            >
                                                Clear all
                                            </button>
                                        </div>
                                    )}
                                </div>
                            )}
                        </div>
                    </div>

                    <Select value={sortBy} onValueChange={setSortBy}>
                        <SelectTrigger className="w-[180px] bg-zinc-950/20 border-border/40 h-10">
                            <SelectValue placeholder="Sort by" />
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

                    {isLoading && !cachedProject ? (
                        <Skeleton className="h-10 w-28" />
                    ) : canEditFlags ? (
                        <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
                            <DialogTrigger asChild>
                                <Button className="cursor-pointer h-10">
                                    <Plus className="mr-2 h-4 w-4" />
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
                                <Form {...createForm}>
                                    <form onSubmit={createForm.handleSubmit(handleCreateFlagSubmit)}>
                                        <div className="py-4">
                                            <FormField
                                                control={createForm.control}
                                                name="key"
                                                render={({ field }) => (
                                                    <FormItem>
                                                        <FormControl>
                                                            <Input
                                                                {...field}
                                                                placeholder="e.g. new-billing-ui"
                                                                autoFocus
                                                                className="font-mono"
                                                            />
                                                        </FormControl>
                                                        <FormMessage />
                                                    </FormItem>
                                                )}
                                            />
                                        </div>
                                        <DialogFooter>
                                            <Button type="button" variant="outline" onClick={() => setIsCreateOpen(false)}>Cancel</Button>
                                            <Button type="submit" disabled={createFlag.isPending}>
                                                {createFlag.isPending ? 'Creating...' : 'Create Flag'}
                                            </Button>
                                        </DialogFooter>
                                    </form>
                                </Form>
                            </DialogContent>
                        </Dialog>
                    ) : null}
                </div>


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
                <Card className="border-border/40 bg-zinc-950/20">
                    <Table wrapperClassName="max-h-[calc(100vh-260px)] overflow-auto">
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
                            <TableSkeleton columnsCount={5} rowsCount={3} />
                        </TableBody>
                    </Table>
                </Card>
            )}
        </div>
    );
}