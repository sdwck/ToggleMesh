import { createBrowserRouter, Navigate } from 'react-router-dom';
import { Login } from '@/features/auth/Login';
import { Register } from '@/features/auth/Register';
import { AppLayout } from '@/components/layout/AppLayout';
import { ProjectsPage } from '@/features/projects/ProjectsPage';
import { ProjectFlagsPage } from '@/features/projects/ProjectFlagsPage';
import { ProjectEnvironmentsPage } from '@/features/projects/ProjectEnvironmentsPage';
import { ProjectMembersPage } from '@/features/projects/ProjectMembersPage';
import { ProjectAuditPage } from '@/features/projects/ProjectAuditPage';
import { ProjectFlagDetailsPage } from '@/features/flags/ProjectFlagDetailsPage';
import { EnvironmentDetailsPage } from '@/features/environments/EnvironmentDetailsPage';
import { ProjectSettingsPage } from '@/features/projects/ProjectSettingsPage';
import { AccountSettingsPage } from '@/features/auth/AccountSettingsPage';
import { OrganizationSettingsPage } from '@/features/organizations/OrganizationSettingsPage';
import { GlobalError } from '@/components/GlobalError';
import { PlaygroundPage } from "@/PlaygroundPage.tsx";

export const router = createBrowserRouter([
    {
        path: '/login',
        element: <Login />,
        errorElement: <GlobalError />,
    },
    {
        path: '/register',
        element: <Register />,
        errorElement: <GlobalError />,
    },
    {
        path: '/',
        element: <AppLayout />,
        errorElement: <GlobalError />,
        children: [
            {
                index: true,
                element: <Navigate to="/projects" replace />,
            },
            {
                path: 'projects',
                element: <ProjectsPage />,
            },
            {
                path: 'settings/account',
                element: <AccountSettingsPage />,
            },
            {
                path: 'settings/organization',
                element: <OrganizationSettingsPage />,
            },
            {
                path: 'projects/:projectId',
                element: <Navigate to="flags" replace />,
            },
            {
                path: 'projects/:projectId/flags',
                element: <ProjectFlagsPage />,
            },
            {
                path: 'projects/:projectId/environments',
                element: <ProjectEnvironmentsPage />,
            },
            {
                path: 'projects/:projectId/members',
                element: <ProjectMembersPage />,
            },
            {
                path: 'projects/:projectId/audit',
                element: <ProjectAuditPage />,
            },
            {
                path: 'projects/:projectId/settings',
                element: <ProjectSettingsPage />,
            },
            {
                path: 'projects/:projectId/environments/:environmentId',
                element: <EnvironmentDetailsPage />,
            },
            {
                path: 'projects/:projectId/flags/:flagKey',
                element: <ProjectFlagDetailsPage />,
            },
            {
                path: 'projects/:projectId/playground',
                element: <PlaygroundPage />,
            },
        ],
    },
]);