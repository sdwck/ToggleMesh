import { useEffect, useState } from 'react';
import { Link, useSearchParams, useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Shield, ArrowLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { toast } from 'sonner';
import api from '@/api/axios';
import { useOrganizationStore } from '@/stores/useOrganizationStore';

export function Register() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const inviteToken = searchParams.get('inviteToken');
  const inviteEmail = searchParams.get('email');

  const [email, setEmail] = useState(inviteEmail || '');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [isSuccess, setIsSuccess] = useState(false);
  const [ssoEnabled, setSsoEnabled] = useState(false);

  useEffect(() => {
    api.get<{ enabled: boolean }>('/auth/sso/status')
      .then((res) => setSsoEnabled(res.data.enabled))
      .catch(() => setSsoEnabled(false));
  }, []);

  const handleSsoClick = () => {
    if (inviteToken) {
      localStorage.setItem('pendingInviteToken', inviteToken);
    }
    const isDev = import.meta.env.DEV;
    const baseUrl = import.meta.env.VITE_API_URL || (isDev ? 'https://localhost:7282/api/v1' : '/api/v1');
    window.location.href = `${baseUrl.replace(/\/$/, '')}/auth/sso/login`;
  };

  const registerMutation = useMutation({
    mutationFn: async () => {
      const response = await api.post('/auth/register', { email, password, inviteToken });
      return response.data;
    },
    onSuccess: async (data: any) => {
      if (inviteToken && data?.token) {
        localStorage.setItem('accessToken', data.token);
        localStorage.setItem('refreshToken', data.refreshToken);
        try {
            const res = await api.post(`/organizations/invites/${inviteToken}/accept`, {});
            useOrganizationStore.getState().setActiveOrganizationId(res.data.organizationId);
            queryClient.invalidateQueries({ queryKey: ['organizations'] });
            queryClient.invalidateQueries({ queryKey: ['projects'] });
            
            toast.success('Account created and invitation accepted!');
            navigate('/projects');
        } catch (error) {
            navigate(`/invites/${inviteToken}`);
        }
      } else {
        setIsSuccess(true);
        toast.success('Account created! Please check your email.');
      }
    },
    onError: (error: any) => {
      const message = error.response?.data?.message || 'Failed to create account. Please check your details.';
      toast.error(message);
    }
  });

  const handleRegisterSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (password !== confirmPassword) {
      toast.error('Passwords do not match');
      return;
    }
    if (password.length < 6) {
      toast.error('Password must be at least 6 characters');
      return;
    }
    registerMutation.mutate();
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-background p-4 relative">
      <div className="absolute top-8 left-8">
        <Button variant="ghost" size="sm" className="text-muted-foreground" asChild>
          <Link to={`/login${inviteToken ? `?inviteToken=${inviteToken}` : ''}`}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back to login
          </Link>
        </Button>
      </div>

      <Card className="w-full max-w-md border-border/40 shadow-2xl">
        <CardHeader className="space-y-2 text-center pb-8">
          <div className="flex justify-center mb-4">
            <div className="h-12 w-12 rounded-xl bg-primary/10 flex items-center justify-center">
              <Shield className="h-6 w-6 text-primary" />
            </div>
          </div>
          <CardTitle className="text-2xl font-bold tracking-tight">Create an account</CardTitle>
          <CardDescription className="text-muted-foreground">
            Get started with ToggleMesh to manage your feature flags.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {isSuccess ? (
            <div className="text-center space-y-4">
              <div className="bg-primary/10 text-primary p-4 rounded-lg inline-block mb-2">
                <Shield className="h-8 w-8" />
              </div>
              <h3 className="text-xl font-medium">Check your email</h3>
              <p className="text-muted-foreground">
                We've sent a confirmation link to <strong>{email}</strong>. Please click the link to verify your account before logging in.
              </p>
              <Button className="w-full mt-4" asChild>
                <Link to="/login">Go to Login</Link>
              </Button>
            </div>
          ) : (
            <form onSubmit={handleRegisterSubmit} className="space-y-4">
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
                disabled={registerMutation.isPending || !!inviteEmail}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                placeholder="At least 6 characters"
                value={password}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) => setPassword(e.target.value)}
                required
                className="bg-muted/50 border-border/50 focus-visible:ring-1"
                disabled={registerMutation.isPending}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="confirmPassword">Confirm Password</Label>
              <Input
                id="confirmPassword"
                type="password"
                value={confirmPassword}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) => setConfirmPassword(e.target.value)}
                required
                className="bg-muted/50 border-border/50 focus-visible:ring-1"
                disabled={registerMutation.isPending}
              />
            </div>
            <Button
              type="submit"
              className="w-full h-11 text-primary-foreground font-medium mt-4"
              disabled={registerMutation.isPending}
            >
              {registerMutation.isPending ? 'Creating account...' : 'Create account'}
            </Button>
          </form>
          )}

          {!isSuccess && ssoEnabled && (
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
      </Card>
    </div>
  );
}