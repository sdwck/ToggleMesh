import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  InputOTP,
  InputOTPGroup,
  InputOTPSeparator,
  InputOTPSlot,
} from "@/components/ui/input-otp";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { toast } from 'sonner';
import { ToggleMeshIcon } from '@/components/icons/ToggleMeshIcon';
import { useOrganizationStore } from '@/stores/useOrganizationStore';
import api from '@/api/axios';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { handleApiError, toastApiError } from '@/api/errorUtils';
import { useSystemConfig } from '@/api/queries';

const loginSchema = z.object({
  email: z.string().min(1, 'Email is required').email('Invalid email format'),
  password: z.string().min(1, 'Password is required'),
});

type LoginValues = z.infer<typeof loginSchema>;

interface LoginResponse {
  token: string;
  refreshToken: string;
  requiresTwoFactor?: boolean;
  twoFactorToken?: string;
}

export function Login() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { data: systemConfig } = useSystemConfig();
  const [ssoEnabled, setSsoEnabled] = useState(false);
  const [isSsoExchanging, setIsSsoExchanging] = useState(false);
  const [ssoError, setSsoError] = useState<string | null>(null);
  const [requiresTwoFactor, setRequiresTwoFactor] = useState(false);
  const [twoFactorToken, setTwoFactorToken] = useState<string | null>(null);
  const [twoFactorCode, setTwoFactorCode] = useState('');
  const [isUsingRecoveryCode, setIsUsingRecoveryCode] = useState(false);

  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  });

  const ssoExchangedRef = useRef(false);

  useEffect(() => {
    api.get<{ enabled: boolean }>('/auth/sso/status')
      .then((res) => setSsoEnabled(res.data.enabled))
      .catch(() => setSsoEnabled(false));

    const params = new URLSearchParams(window.location.search);
    const ticket = params.get('ticket');
    if (ticket && !ssoExchangedRef.current) {
      ssoExchangedRef.current = true;
      window.history.replaceState(null, '', window.location.pathname);
      setIsSsoExchanging(true);
      setSsoError(null);

      api.post<LoginResponse>('/auth/sso/exchange', { ticket })
        .then(async (res) => {
          if (res.data.requiresTwoFactor && res.data.twoFactorToken) {
            setRequiresTwoFactor(true);
            setTwoFactorToken(res.data.twoFactorToken);
            setIsSsoExchanging(false);
            return;
          }

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
              toastApiError(error, 'Failed to accept invitation via SSO');
            } finally {
              localStorage.removeItem('pendingInviteToken');
            }
          }

          navigate(pendingInviteToken ? '/projects' : '/');
        })
        .catch((err) => {
          toastApiError(err, 'Failed to complete SSO authentication. The link may have expired.');
          setSsoError('Failed to complete SSO authentication. The link may have expired.');
        })
        .finally(() => {
          setIsSsoExchanging(false);
        });
    }
  }, [navigate, queryClient]);

  const loginMutation = useMutation({
    mutationFn: async (values: LoginValues) => {
      const response = await api.post<LoginResponse>('/auth/login', values);
      return response.data;
    },
    onSuccess: async (data) => {
      if (data.requiresTwoFactor && data.twoFactorToken) {
        setRequiresTwoFactor(true);
        setTwoFactorToken(data.twoFactorToken);
        return;
      }

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
          toastApiError(error, 'Failed to accept invitation');
        } finally {
          localStorage.removeItem('pendingInviteToken');
        }
      }

      navigate(inviteToken ? '/projects' : '/');
    },
    onError: (error: any) => {
      if (error.response?.status === 401) {
        form.setError('root', { type: 'server', message: 'Invalid email or password.' });
      } else {
        handleApiError(error, form.setError, 'Failed to login');
      }
    }
  });

  const verifyTwoFactorMutation = useMutation({
    mutationFn: async (code: string) => {
      const response = await api.post<LoginResponse>('/auth/login/2fa', {
        twoFactorToken,
        code
      });
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
          toastApiError(error, 'Failed to accept invitation');
        } finally {
          localStorage.removeItem('pendingInviteToken');
        }
      }

      navigate(inviteToken ? '/projects' : '/');
    },
    onError: (error: any) => {
      toastApiError(error, 'Invalid two-factor code');
    }
  });

  const onSubmit = (values: LoginValues) => {
    loginMutation.mutate(values);
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

  if (requiresTwoFactor) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background p-4">
        <Card className="w-full max-w-md border-border/40 shadow-2xl">
          <CardHeader className="space-y-2 text-center pb-8">
            <div className="flex justify-center mb-4">
              <div className="h-14 w-14 rounded-xl flex items-center justify-center bg-zinc-950 border border-border/40 shadow-sm transition-shadow">
                <ToggleMeshIcon className="h-8 w-8 text-zinc-300 transition-colors duration-300" />
              </div>
            </div>
            <CardTitle className="text-2xl font-bold tracking-tight">Two-Factor Authentication</CardTitle>
            <CardDescription className="text-muted-foreground">
              {isUsingRecoveryCode ? 'Enter one of your 10-character recovery codes.' : 'Enter the 6-digit code from your authenticator app.'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={(e) => { e.preventDefault(); verifyTwoFactorMutation.mutate(twoFactorCode); }} className="space-y-4 flex flex-col items-center w-full">
              <div className="space-y-2 flex flex-col items-center w-full">
                <label className="text-sm font-medium text-foreground text-center w-full">
                    {isUsingRecoveryCode ? 'Recovery Code' : 'Authentication Code'}
                </label>
                
                {isUsingRecoveryCode ? (
                    <Input 
                        value={twoFactorCode} 
                        onChange={(e) => setTwoFactorCode(e.target.value)} 
                        disabled={verifyTwoFactorMutation.isPending} 
                        placeholder="e.g. XXXXX-XXXXX"
                        className="text-center tracking-widest uppercase font-mono max-w-[250px]"
                        autoFocus
                    />
                ) : (
                    <InputOTP maxLength={6} value={twoFactorCode} onChange={setTwoFactorCode} disabled={verifyTwoFactorMutation.isPending} autoFocus>
                    <InputOTPGroup>
                        <InputOTPSlot index={0} />
                        <InputOTPSlot index={1} />
                        <InputOTPSlot index={2} />
                    </InputOTPGroup>
                    <InputOTPSeparator />
                    <InputOTPGroup>
                        <InputOTPSlot index={3} />
                        <InputOTPSlot index={4} />
                        <InputOTPSlot index={5} />
                    </InputOTPGroup>
                    </InputOTP>
                )}
              </div>
              <Button
                type="submit"
                className="w-full h-11 text-primary-foreground font-medium mt-4"
                disabled={verifyTwoFactorMutation.isPending || (isUsingRecoveryCode ? twoFactorCode.length < 10 : twoFactorCode.length < 6)}
              >
                {verifyTwoFactorMutation.isPending ? 'Verifying...' : 'Verify'}
              </Button>
              <div className="flex flex-col gap-2 w-full mt-2">
                  <Button
                    type="button"
                    variant="ghost"
                    className="w-full text-sm text-muted-foreground"
                    onClick={() => {
                        setIsUsingRecoveryCode(!isUsingRecoveryCode);
                        setTwoFactorCode('');
                    }}
                    disabled={verifyTwoFactorMutation.isPending}
                  >
                    {isUsingRecoveryCode ? 'Use authenticator app' : 'Use recovery code instead'}
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    className="w-full text-sm text-muted-foreground"
                    onClick={() => { setRequiresTwoFactor(false); setTwoFactorToken(null); setTwoFactorCode(''); setIsUsingRecoveryCode(false); }}
                    disabled={verifyTwoFactorMutation.isPending}
                  >
                    Back to login
                  </Button>
              </div>
            </form>
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
            <div className="h-14 w-14 rounded-xl flex items-center justify-center bg-zinc-950 border border-border/40 shadow-sm transition-shadow">
              <ToggleMeshIcon className="h-8 w-8 text-zinc-300 transition-colors duration-300" />
            </div>
          </div>
          <CardTitle className="text-2xl font-bold tracking-tight">Welcome back</CardTitle>
          <CardDescription className="text-muted-foreground">
            Sign in to manage your feature flags and environments.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
              <FormField
                control={form.control}
                name="email"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Work Email</FormLabel>
                    <FormControl>
                      <Input
                        placeholder="name@company.com"
                        type="email"
                        {...field}
                        className="bg-muted/50 border-border/50 focus-visible:ring-1"
                        disabled={loginMutation.isPending}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="password"
                render={({ field }) => (
                  <FormItem>
                    <div className="flex items-center justify-between">
                      <FormLabel>Password</FormLabel>
                      <a href="/forgot-password" className="text-xs text-primary hover:underline font-medium">
                        Forgot password?
                      </a>
                    </div>
                    <FormControl>
                      <Input
                        type="password"
                        placeholder="••••••••"
                        {...field}
                        className="bg-muted/50 border-border/50 focus-visible:ring-1"
                        disabled={loginMutation.isPending}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {form.formState.errors.root && (
                <div className="text-sm text-destructive font-medium">
                  {form.formState.errors.root.message}
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
          </Form>

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
        
        {systemConfig?.allowOpenRegistration !== false && (
          <CardFooter className="flex justify-center border-t border-border/40 pt-6">
            <p className="text-sm text-muted-foreground">
              Don't have an account?{' '}
              <a href="/register" className="text-primary hover:underline font-medium">
                Create an account
              </a>
            </p>
          </CardFooter>
        )}
      </Card>
    </div>
  );
}