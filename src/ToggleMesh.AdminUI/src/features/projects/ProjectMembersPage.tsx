import { useParams } from 'react-router-dom';
import { useProjectDetails } from '@/api/queries';
import { ProjectMembersTab } from './components/ProjectMembersTab';
import { Skeleton } from '@/components/ui/skeleton';
import { ProjectRole } from '@/api/types';

export function ProjectMembersPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const { data: project, isLoading } = useProjectDetails(projectId!);

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-4 w-96" />
        <Skeleton className="h-[200px] w-full" />
      </div>
    );
  }

  if (!project) return <div>Project not found</div>;

  const canManageProject = project.userRole === ProjectRole.Owner || project.userRole === ProjectRole.Admin;

  if (!canManageProject) {
    return (
      <div className="p-6 border border-destructive/20 bg-destructive/10 rounded-md text-destructive">
        You do not have permission to manage members of this project.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <ProjectMembersTab project={project} />
    </div>
  );
}
