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
import { ProjectDashboardPage } from '@/features/projects/ProjectDashboardPage';
import { ProjectExperimentsPage } from '@/features/experiments/ProjectExperimentsPage';
import { TerminalPage } from '@/features/terminal/TerminalPage';
import { AccountSettingsPage } from '@/features/auth/AccountSettingsPage';
import { OrganizationSettingsPage } from '@/features/organizations/OrganizationSettingsPage';
import { GlobalError } from '@/components/GlobalError';
import { PlaygroundPage } from "@/features/playground/PlaygroundPage.tsx";

import { ConfirmEmailPage } from '@/features/auth/ConfirmEmailPage';
import { InvitePage } from '@/features/organizations/InvitePage';
import { ForgotPasswordPage } from '@/features/auth/ForgotPasswordPage';
import { ResetPasswordPage } from '@/features/auth/ResetPasswordPage';
import { SupportPage } from '@/features/support/SupportPage';

import { AppRoot } from '@/components/layout/AppRoot';

export const router = createBrowserRouter([
    {
        element: <AppRoot />,
        children: [
            {
                path: '/support',
                element: <SupportPage />,
                errorElement: <GlobalError />,
            },
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
                path: '/forgot-password',
                element: <ForgotPasswordPage />,
                errorElement: <GlobalError />,
            },
            {
                path: '/auth/reset-password',
                element: <ResetPasswordPage />,
                errorElement: <GlobalError />,
            },
            {
                path: '/auth/confirm-email',
                element: <ConfirmEmailPage />,
                errorElement: <GlobalError />,
            },
            {
                path: '/invites/:token',
                element: <InvitePage />,
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
                        element: <ProjectDashboardPage />,
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
                        path: 'projects/:projectId/experiments',
                        element: <ProjectExperimentsPage />,
                    },
                    {
                        path: 'projects/:projectId/terminal',
                        element: <TerminalPage />,
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
            }
        ]
    }
]);