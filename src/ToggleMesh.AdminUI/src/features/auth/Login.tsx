import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { Shield } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { toast } from 'sonner';
import { useQueryClient } from '@tanstack/react-query';
import { useOrganizationStore } from '@/stores/useOrganizationStore';
import api from '@/api/axios';

interface LoginResponse {
  token: string;
  refreshToken: string;
}

export function Login() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [ssoEnabled, setSsoEnabled] = useState(false);
  const [isSsoExchanging, setIsSsoExchanging] = useState(false);
  const [ssoError, setSsoError] = useState<string | null>(null);

  useEffect(() => {
    api.get<{ enabled: boolean }>('/auth/sso/status')
      .then((res) => setSsoEnabled(res.data.enabled))
      .catch(() => setSsoEnabled(false));

    const params = new URLSearchParams(window.location.search);
    const ticket = params.get('ticket');
    if (ticket) {
      setIsSsoExchanging(true);
      setSsoError(null);

      api.post<LoginResponse>('/auth/sso/exchange', { ticket })
        .then(async (res) => {
          localStorage.setItem('accessToken', res.data.token);
          localStorage.setItem('refreshToken', res.data.refreshToken);

          const pendingInviteToken = localStorage.getItem('pendingInviteToken');
          if (pendingInviteToken) {
            try {
              const acceptRes = await api.post(`/organizations/invites/${pendingInviteToken}/accept`, {});
              useOrganizationStore.getState().setActiveOrganizationId(acceptRes.data.organizationId);
              queryClient.invalidateQueries({ queryKey: ['organizations'] });
              queryClient.invalidateQueries({ queryKey: ['projects'] });
              toast.success('Invitation accepted via SSO!');
            } catch (error: any) {
              toast.error(error.response?.data?.errors?.[0]?.message || 'Failed to accept invitation via SSO');
            } finally {
              localStorage.removeItem('pendingInviteToken');
            }
          }

          window.history.replaceState(null, '', window.location.pathname);
          navigate(pendingInviteToken ? '/projects' : '/');
        })
        .catch((err) => {
          console.error('SSO Exchange error:', err);
          setSsoError('Failed to complete SSO authentication. The link may have expired.');
        })
        .finally(() => {
          setIsSsoExchanging(false);
        });
    }
  }, [navigate]);

  const loginMutation = useMutation({
    mutationFn: async () => {
      const response = await api.post<LoginResponse>('/auth/login', { email, password });
      return response.data;
    },
    onSuccess: async (data) => {
      localStorage.setItem('accessToken', data.token);
      localStorage.setItem('refreshToken', data.refreshToken);

      const params = new URLSearchParams(window.location.search);
      const inviteToken = params.get('inviteToken') || localStorage.getItem('pendingInviteToken');

      if (inviteToken) {
        try {
          const acceptRes = await api.post(`/organizations/invites/${inviteToken}/accept`, {});
          useOrganizationStore.getState().setActiveOrganizationId(acceptRes.data.organizationId);
          queryClient.invalidateQueries({ queryKey: ['organizations'] });
          queryClient.invalidateQueries({ queryKey: ['projects'] });
          toast.success('Invitation accepted!');
        } catch (error: any) {
          toast.error(error.response?.data?.errors?.[0]?.message || 'Failed to accept invitation');
        } finally {
          localStorage.removeItem('pendingInviteToken');
        }
      }

      navigate(inviteToken ? '/projects' : '/');
    },
  });

  const handleLoginSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    loginMutation.mutate();
  };

  const handleSsoClick = () => {
    const params = new URLSearchParams(window.location.search);
    const inviteToken = params.get('inviteToken');
    if (inviteToken) {
      localStorage.setItem('pendingInviteToken', inviteToken);
    }
    const isDev = import.meta.env.DEV;
    const baseUrl = import.meta.env.VITE_API_URL || (isDev ? 'https://localhost:7282/api/v1' : '/api/v1');
    window.location.href = `${baseUrl.replace(/\/$/, '')}/auth/sso/login`;
  };

  if (isSsoExchanging) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background p-4">
        <Card className="w-full max-w-md border-border/40 shadow-2xl">
          <CardContent className="flex flex-col items-center justify-center py-12 space-y-4">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
            <p className="text-muted-foreground text-sm font-medium">Completing SSO authentication...</p>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-background p-4">
      <Card className="w-full max-w-md border-border/40 shadow-2xl">
        <CardHeader className="space-y-2 text-center pb-8">
          <div className="flex justify-center mb-4">
            <div className="h-12 w-12 rounded-xl bg-primary/10 flex items-center justify-center">
              <Shield className="h-6 w-6 text-primary" />
            </div>
          </div>
          <CardTitle className="text-2xl font-bold tracking-tight">Welcome back</CardTitle>
          <CardDescription className="text-muted-foreground">
            Sign in to manage your feature flags and environments.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleLoginSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="email">Work Email</Label>
              <Input
                id="email"
                type="email"
                placeholder="name@company.com"
                value={email}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) => setEmail(e.target.value)}
                required
                className="bg-muted/50 border-border/50 focus-visible:ring-1"
                disabled={loginMutation.isPending}
              />
            </div>
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Label htmlFor="password">Password</Label>
              </div>
              <Input
                id="password"
                type="password"
                value={password}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) => setPassword(e.target.value)}
                required
                className="bg-muted/50 border-border/50 focus-visible:ring-1"
                disabled={loginMutation.isPending}
              />
            </div>
            {loginMutation.isError && (
              <div className="text-sm text-destructive font-medium">
                Invalid email or password. Please try again.
              </div>
            )}
            {ssoError && (
              <div className="text-sm text-destructive font-medium">
                {ssoError}
              </div>
            )}
            <Button
              type="submit"
              className="w-full h-11 text-primary-foreground font-medium mt-4"
              disabled={loginMutation.isPending}
            >
              {loginMutation.isPending ? 'Signing in...' : 'Sign in'}
            </Button>
          </form>

          {ssoEnabled && (
            <div className="space-y-4 mt-6">
              <div className="relative">
                <div className="absolute inset-0 flex items-center">
                  <span className="w-full border-t border-border/40" />
                </div>
                <div className="relative flex justify-center text-xs uppercase">
                  <span className="bg-background px-2 text-muted-foreground">Or continue with</span>
                </div>
              </div>
              <Button
                type="button"
                variant="outline"
                className="w-full h-11 font-medium bg-muted/20 border-border/50 hover:bg-muted/50 transition-colors"
                onClick={handleSsoClick}
              >
                Sign in with SSO
              </Button>
            </div>
          )}
        </CardContent>
        <CardFooter className="flex justify-center border-t border-border/40 pt-6">
          <p className="text-sm text-muted-foreground">
            Don't have an account?{' '}
            <a href="/register" className="text-primary hover:underline font-medium">
              Create an account
            </a>
          </p>
        </CardFooter>
      </Card>
    </div>
  );
}