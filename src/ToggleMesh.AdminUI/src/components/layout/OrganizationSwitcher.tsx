import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Check, ChevronsUpDown, Plus } from 'lucide-react';
import { useOrganizations, useCreateOrganization } from '@/api/queries';
import { useOrganizationStore } from '@/stores/useOrganizationStore';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { toast } from 'sonner';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';

export function OrganizationSwitcher() {
    const navigate = useNavigate();
    const { data: organizations, isLoading } = useOrganizations();
    const createOrganization = useCreateOrganization();
    const { activeOrganizationId, setActiveOrganizationId } = useOrganizationStore();
    
    const [isCreateOpen, setIsCreateOpen] = useState(false);
    const [newOrgName, setNewOrgName] = useState('');
    const [dropdownOpen, setDropdownOpen] = useState(false);

    useEffect(() => {
        if (organizations?.length && !activeOrganizationId) {
            setActiveOrganizationId(organizations[0].id);
        }
    }, [organizations, activeOrganizationId, setActiveOrganizationId]);

    if (isLoading) {
        return <Skeleton className="h-9 w-[150px]" />;
    }

    const activeOrg = organizations?.find(o => o.id === activeOrganizationId) || organizations?.[0];

    const handleCreate = async () => {
        if (!newOrgName.trim()) return;
        try {
            const result = await createOrganization.mutateAsync({ name: newOrgName });
            setActiveOrganizationId(result.id);
            toast.success('Organization created successfully');
            setNewOrgName('');
            setIsCreateOpen(false);
            navigate('/projects');
        } catch {
            toast.error('Failed to create organization');
        }
    };

    return (
        <>
            <DropdownMenu open={dropdownOpen} onOpenChange={setDropdownOpen}>
                <DropdownMenuTrigger asChild>
                    <Button variant="ghost" role="combobox" className="h-9 px-2 text-lg font-bold tracking-tight hover:bg-zinc-900/50 flex items-center gap-1.5 focus-visible:ring-0">
                        <span className="truncate">{activeOrg?.name || 'Select Organization...'}</span>
                        <ChevronsUpDown className="h-4 w-4 shrink-0 opacity-50" />
                    </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="start" className="w-[220px] border-border/40 bg-zinc-950/95 backdrop-blur-xl z-[100]">
                    <DropdownMenuLabel className="text-xs text-muted-foreground font-semibold">Organizations</DropdownMenuLabel>
                    <div style={organizations && organizations.length > 10 ? { maxHeight: '320px', overflowY: 'auto' } : undefined}>
                        {organizations?.map((org) => (
                            <DropdownMenuItem
                                key={org.id}
                                onSelect={() => {
                                    setActiveOrganizationId(org.id);
                                    navigate('/projects');
                                }}
                                className="flex items-center justify-between cursor-pointer focus:bg-primary/10"
                            >
                                <span className="truncate">{org.name}</span>
                                {activeOrganizationId === org.id && (
                                    <Check className="h-4 w-4 shrink-0 text-primary" />
                                )}
                            </DropdownMenuItem>
                        ))}
                    </div>
                    
                    <DropdownMenuSeparator className="bg-border/40" />
                    <DropdownMenuItem 
                        onSelect={(e) => {
                            e.preventDefault();
                            setIsCreateOpen(true);
                            setDropdownOpen(false);
                        }}
                        className="cursor-pointer text-muted-foreground focus:text-foreground"
                    >
                        <Plus className="mr-2 h-4 w-4" />
                        <span>Create Organization</span>
                    </DropdownMenuItem>
                </DropdownMenuContent>
            </DropdownMenu>

            <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
                <DialogContent className="border-border/40 bg-zinc-950">
                    <DialogHeader>
                        <DialogTitle>Create Organization</DialogTitle>
                        <DialogDescription>
                            An organization contains your projects and team members.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="py-4">
                        <Input
                            placeholder="e.g., Acme Corp"
                            value={newOrgName}
                            onChange={(e) => setNewOrgName(e.target.value)}
                            autoFocus
                        />
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setIsCreateOpen(false)}>Cancel</Button>
                        <Button onClick={handleCreate} disabled={createOrganization.isPending || !newOrgName.trim()}>
                            {createOrganization.isPending ? 'Creating...' : 'Create'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </>
    );
}
