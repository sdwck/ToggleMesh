import { useParams, useNavigate, useSearchParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { useProjectDetails } from '@/api/queries';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ProjectRole } from '@/api/types';
import { Skeleton } from '@/components/ui/skeleton';

import { ProjectFlagsTab } from './ProjectFlagsTab';
import { ProjectEnvironmentsTab } from './components/ProjectEnvironmentsTab';
import { ProjectMembersTab } from './components/ProjectMembersTab';
import { ProjectAuditTab } from './components/ProjectAuditTab';

export function ProjectDetailsPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { data: project, isLoading } = useProjectDetails(projectId!);

  const activeTab = searchParams.get('tab') || 'flags';

  const handleTabChange = (value: string) => {
    setSearchParams({ tab: value }, { replace: true });
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-2">
          <Skeleton className="h-8 w-8" />
          <Skeleton className="h-8 w-32" />
        </div>
        <div>
          <Skeleton className="h-8 w-64 mb-2" />
          <Skeleton className="h-4 w-96" />
        </div>
        <Skeleton className="h-10 w-64 mt-6" />
        <div className="grid gap-6 mt-6">
          <Skeleton className="h-[120px] w-full" />
          <Skeleton className="h-[120px] w-full" />
        </div>
      </div>
    );
  }

  if (!project) return <div>Project not found</div>;

  const canManageProject = project.userRole === ProjectRole.Owner || project.userRole === ProjectRole.Admin;

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" className="-ml-3 text-muted-foreground" onClick={() => navigate('/projects')}>
        <ArrowLeft className="mr-2 h-4 w-4" />
        Back to Projects
      </Button>

      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">{project.name}</h2>
          <p className="text-muted-foreground">Manage environments and team members.</p>
        </div>
      </div>

      <Tabs value={activeTab} onValueChange={handleTabChange} className="space-y-6">
        <TabsList>
          <TabsTrigger value="flags">Feature Flags</TabsTrigger>
          <TabsTrigger value="environments">Environments</TabsTrigger>
          {canManageProject && <TabsTrigger value="members">Members</TabsTrigger>}
          <TabsTrigger value="audit">Audit Logs</TabsTrigger>
        </TabsList>

        <TabsContent value="flags" className="m-0">
          <ProjectFlagsTab project={project} />
        </TabsContent>

        <TabsContent value="environments" className="space-y-6 m-0">
          <ProjectEnvironmentsTab project={project} />
        </TabsContent>

        {canManageProject && (
          <TabsContent value="members" className="space-y-6 m-0">
            <ProjectMembersTab project={project} />
          </TabsContent>
        )}

        <TabsContent value="audit" className="space-y-6 m-0">
          <ProjectAuditTab project={project} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
