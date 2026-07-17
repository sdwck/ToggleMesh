import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router-dom';
import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import { router } from './router';
import { Toaster } from 'sonner';
import './index.css';
import {hydrateCache, persistCache} from "@/api/persistentCache.ts";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (failureCount, error: any) => {
        const status = error?.response?.status;
        if (status === 401 || status === 403 || status === 404 || status === 400) {
          return false;
        }
        return failureCount < 1;
      },
      refetchOnWindowFocus: false,
    },
  },
});

hydrateCache(queryClient).then(() => {
  persistCache(queryClient);

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
        <Toaster theme="dark" position="bottom-right" />
      </QueryClientProvider>
    </StrictMode>
  );
});