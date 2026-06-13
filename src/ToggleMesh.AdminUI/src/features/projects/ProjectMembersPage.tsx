import {useParams} from 'react-router-dom';
import {useProjectDetails} from '@/api/queries';
import {ProjectMembersTab} from './components/ProjectMembersTab';
import {ProjectRole} from '@/api/types';

export function ProjectMembersPage() {
    const {projectId} = useParams<{ projectId: string }>();
    const {data: project, isLoading} = useProjectDetails(projectId!);

    if (!project && !isLoading) return <div className="p-8 text-center text-muted-foreground">Project not found</div>;
    const canManageProject = project ? (project.userRole === ProjectRole.Owner || project.userRole === ProjectRole.Admin) : false;

    if (!canManageProject && !isLoading) {
        return (
            <div className="p-6 border border-destructive/20 bg-destructive/10 rounded-md text-destructive">
                You do not have permission to manage members of this project.
            </div>
        );
    }

    return (
        <div className="space-y-6">
            <ProjectMembersTab project={project} isLoading={isLoading} />
        </div>
    );
}
