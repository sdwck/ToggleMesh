import { useRouteError } from 'react-router-dom';
import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';

export function GlobalError() {
  const error = useRouteError() as Error;

  return (
    <div className="min-h-screen bg-background flex flex-col items-center justify-center p-4 text-center">
      <div className="h-12 w-12 rounded-full bg-destructive/10 flex items-center justify-center mb-4">
        <AlertTriangle className="h-6 w-6 text-destructive" />
      </div>
      <h1 className="text-2xl font-bold tracking-tight mb-2">Something went wrong</h1>
      <p className="text-muted-foreground max-w-md mb-6">
        {error?.message || 'An unexpected error occurred while rendering the page.'}
      </p>
      <Button onClick={() => window.location.href = '/'}>
        Return to Dashboard
      </Button>
    </div>
  );
}