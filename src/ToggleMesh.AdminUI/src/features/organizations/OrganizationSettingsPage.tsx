import { useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useOrganizationStore } from '@/stores/useOrganizationStore';
import {
    useOrganizationMembers,
    useOrganizations
} from '@/api/queries';
import { OrganizationRole } from '@/api/types';
import { Badge } from '@/components/ui/badge';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Building2, Users, ShieldCheck, User } from 'lucide-react';
import { jwtDecode } from 'jwt-decode';
import { OrganizationGeneralTab } from './components/OrganizationGeneralTab';
import { OrganizationMembersTab } from './components/OrganizationMembersTab';

export function OrganizationSettingsPage() {
    const { activeOrganizationId, setActiveOrganizationId } = useOrganizationStore();
    const { data: organizations } = useOrganizations();
    const activeOrg = organizations?.find(o => o.id === activeOrganizationId);
    const isAdmin = activeOrg?.role === OrganizationRole.Admin;

    const { data: members, isLoading } = useOrganizationMembers(isAdmin ? activeOrganizationId : null);
    
    const [searchParams, setSearchParams] = useSearchParams();
    let currentTab = searchParams.get('tab') || 'general';

    if (currentTab === 'members' && !isAdmin) {
        currentTab = 'general';
    }

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
                    <OrganizationGeneralTab 
                        activeOrganizationId={activeOrganizationId}
                        activeOrgName={activeOrg?.name || ''}
                        isAdmin={isAdmin}
                        organizations={organizations || []}
                        setActiveOrganizationId={setActiveOrganizationId}
                    />
                </TabsContent>

                {isAdmin && (
                    <TabsContent value="members" className="outline-none">
                        <OrganizationMembersTab 
                            activeOrganizationId={activeOrganizationId}
                            activeOrgName={activeOrg?.name || ''}
                            userEmail={userEmail}
                        />
                    </TabsContent>
                )}
            </Tabs>
        </div>
    );
}
