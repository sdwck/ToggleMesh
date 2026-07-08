import type { ProjectDetails } from '@/api/types';
import { AuditLogViewer } from '@/features/audit/components/AuditLogViewer';

export function ProjectAuditTab({ project }: { project?: ProjectDetails; isLoading?: boolean }) {
    if (!project) return null;

    return (
        <AuditLogViewer 
            projectId={project.id} 
            pageSize={10}
        />
    );
}