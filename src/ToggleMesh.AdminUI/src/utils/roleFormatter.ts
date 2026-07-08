import { ProjectRole, OrganizationRole } from '@/api/types';

export const getRoleLabel = (role?: ProjectRole | OrganizationRole | number): string => {
    switch (role) {
        case 0: return 'Owner';
        case 1: return 'Admin';
        case 2: return 'Editor';
        case 3: return 'Viewer';
        default: return 'Member';
    }
};
