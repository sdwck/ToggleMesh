import { useParams } from 'react-router-dom';
import { useProjectDetails } from '@/api/queries';
import { ProjectEnvironmentsTab } from './components/ProjectEnvironmentsTab';

export function ProjectEnvironmentsPage() {
  const { projectId } = useParams<{ projectId: string }>();
    const { data: project, isLoading } = useProjectDetails(projectId!);

    if (!project && !isLoading) return <div className="p-8 text-center text-muted-foreground">Project not found</div>;

  return (
      <div className="space-y-6">
          <ProjectEnvironmentsTab project={project} isLoading={isLoading} />
      </div>
  );
}
