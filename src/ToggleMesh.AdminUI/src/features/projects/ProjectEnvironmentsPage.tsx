import { useParams } from 'react-router-dom';
import { useProjectDetails } from '@/api/queries';
import { ProjectEnvironmentsTab } from './components/ProjectEnvironmentsTab';
import { Skeleton } from '@/components/ui/skeleton';

export function ProjectEnvironmentsPage() {
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

  return (
    <div className="space-y-6">
      <ProjectEnvironmentsTab project={project} />
    </div>
  );
}
