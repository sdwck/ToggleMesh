import { useParams } from 'react-router-dom';
import { useProjectDetails } from '@/api/queries';
import { ProjectAuditTab } from './components/ProjectAuditTab';

export function ProjectAuditPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const { data: project, isLoading } = useProjectDetails(projectId!);

  if (!project) return <div>Project not found</div>;

    return (
        <div className="space-y-6 h-full flex flex-col">
            <ProjectAuditTab project={project} isLoading={isLoading} />
        </div>
    );
}
