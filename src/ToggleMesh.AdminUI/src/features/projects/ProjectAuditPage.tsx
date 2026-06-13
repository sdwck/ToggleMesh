import { useParams } from 'react-router-dom';
import { useProjectDetails } from '@/api/queries';
import { ProjectAuditTab } from './components/ProjectAuditTab';
import { Skeleton } from '@/components/ui/skeleton';

export function ProjectAuditPage() {
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
      <div>
        <h2 className="text-2xl font-bold tracking-tight">{project.name}</h2>
        <p className="text-muted-foreground">Review project activity and audit trail.</p>
      </div>
      <div>
        <ProjectAuditTab project={project} />
      </div>
    </div>
  );
}
