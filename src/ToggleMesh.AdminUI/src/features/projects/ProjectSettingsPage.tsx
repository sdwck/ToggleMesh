import { useParams, useSearchParams } from 'react-router-dom';
import {
    useProjectDetails,
    useProjectWebhooks,
    useProjectFlags,
    useProjectMembers
} from '@/api/queries';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Settings } from 'lucide-react';
import { ProjectSettingsGeneralTab } from './components/ProjectSettingsGeneralTab';
import { ProjectSettingsWebhooksTab } from './components/ProjectSettingsWebhooksTab';

export function ProjectSettingsPage() {
    const { projectId } = useParams<{ projectId: string }>();
    const [searchParams, setSearchParams] = useSearchParams();
    const activeTab = searchParams.get('tab') || 'general';

    const { data: project, isLoading: isProjectLoading } = useProjectDetails(projectId!);
    const canManageProject = project?.userRole === 0 || project?.userRole === 1;

    const { data: webhooks, isLoading: isWebhooksLoading } = useProjectWebhooks(projectId!);
    const { data: flags, isLoading: isFlagsLoading } = useProjectFlags(projectId!);
    const { data: members, isLoading: isMembersLoading } = useProjectMembers(projectId!);

    const handleTabChange = (tab: string) => {
        setSearchParams({ tab });
    };

    return (
        <div className="space-y-5 pb-10">
            <div>
                <h2 className="text-2xl font-bold tracking-tight">Project Settings</h2>
                <div className="text-muted-foreground flex items-center gap-1 mt-1">
                    Manage {isProjectLoading ? <Skeleton className="h-4 w-24" /> : <span className="font-semibold text-zinc-300">{project?.name}</span>} configuration, webhooks and integrations.
                </div>
            </div>

            <Tabs value={activeTab} onValueChange={handleTabChange} className="space-y-5">
                <TabsList className="bg-zinc-950 border border-border/40 p-1">
                    <TabsTrigger value="general" className="text-xs">General</TabsTrigger>
                    {canManageProject && (
                        <TabsTrigger value="webhooks" className="text-xs gap-1.5">
                            <Settings className="h-3.5 w-3.5" /> Webhooks
                            {!isWebhooksLoading && (
                                <Badge variant="outline" className="px-1 py-0 text-[10px] bg-zinc-900 border-zinc-800">
                                    {webhooks?.length ?? 0}
                                </Badge>
                            )}
                        </TabsTrigger>
                    )}
                </TabsList>

                <TabsContent value="general" className="m-0">
                    <ProjectSettingsGeneralTab 
                        project={project}
                        isProjectLoading={isProjectLoading}
                        flagsCount={flags?.length || 0}
                        isFlagsLoading={isFlagsLoading}
                        webhooksCount={webhooks?.length || 0}
                        isWebhooksLoading={isWebhooksLoading}
                        membersCount={members?.length || 0}
                        isMembersLoading={isMembersLoading}
                        canManageProject={canManageProject}
                        onTabChange={handleTabChange}
                    />
                </TabsContent>

                <TabsContent value="webhooks" className="space-y-6 m-0">
                    <ProjectSettingsWebhooksTab projectId={projectId!} />
                </TabsContent>
            </Tabs>
        </div>
    );
}