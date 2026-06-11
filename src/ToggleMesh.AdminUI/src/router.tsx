import { createBrowserRouter, Navigate } from 'react-router-dom';
import { Login } from '@/features/auth/Login';
import { Register } from '@/features/auth/Register';
import { AppLayout } from '@/components/layout/AppLayout';
import { ProjectsPage } from '@/features/projects/ProjectsPage';
import { ProjectDetailsPage } from '@/features/projects/ProjectDetailsPage';
import { ProjectFlagDetailsPage } from '@/features/flags/ProjectFlagDetailsPage';
import { EnvironmentDetailsPage } from '@/features/environments/EnvironmentDetailsPage';
import { GlobalError } from '@/components/GlobalError';

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
        path: 'projects/:projectId',
        element: <ProjectDetailsPage />,
      },
      {
        path: 'projects/:projectId/environments/:environmentId',
        element: <EnvironmentDetailsPage />,
      },
      {
        path: 'projects/:projectId/flags/:flagKey',
        element: <ProjectFlagDetailsPage />,
      },
    ],
  },
]);