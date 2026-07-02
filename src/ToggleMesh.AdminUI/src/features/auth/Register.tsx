import { useEffect, useState } from 'react';
import { Link, useSearchParams, useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Shield, ArrowLeft } from 'lucide-react';
import { ToggleMeshIcon } from '@/components/icons/ToggleMeshIcon';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { toast } from 'sonner';
import api from '@/api/axios';
import { useOrganizationStore } from '@/stores/useOrganizationStore';
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
import { handleApiError } from '@/api/errorUtils';
import { useSystemConfig } from '@/api/queries';

const getRegisterSchema = (policy: { minimumLength: number, requireDigit: boolean, requireLowercase: boolean, requireUppercase: boolean, requireNonAlphanumeric: boolean }) => {
  let passwordSchema = z.string().min(policy.minimumLength, `Password must be at least ${policy.minimumLength} characters`);

  if (policy.requireUppercase) {
    passwordSchema = passwordSchema.regex(/[A-Z]/, 'Password must contain at least one uppercase letter');
  }
  if (policy.requireLowercase) {
    passwordSchema = passwordSchema.regex(/[a-z]/, 'Password must contain at least one lowercase letter');
  }
  if (policy.requireDigit) {
    passwordSchema = passwordSchema.regex(/[0-9]/, 'Password must contain at least one number');
  }
  if (policy.requireNonAlphanumeric) {
    passwordSchema = passwordSchema.regex(/[^a-zA-Z0-9]/, 'Password must contain at least one special character');
  }

  return z.object({
    email: z.string().min(1, 'Email is required').email('Invalid email format'),
    password: passwordSchema,
    confirmPassword: z.string(),
  }).refine((data) => data.password === data.confirmPassword, {
    message: "Passwords do not match",
    path: ["confirmPassword"],
  });
};

type RegisterValues = z.infer<ReturnType<typeof getRegisterSchema>>;

export function Register() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { data: systemConfig } = useSystemConfig();
  const inviteToken = searchParams.get('inviteToken');
  const inviteEmail = searchParams.get('email');

  const [isSuccess, setIsSuccess] = useState(false);
  const [ssoEnabled, setSsoEnabled] = useState(false);

  const defaultPolicy = { minimumLength: 8, requireDigit: true, requireLowercase: true, requireUppercase: true, requireNonAlphanumeric: true };

  const form = useForm<RegisterValues>({
    resolver: zodResolver(getRegisterSchema(systemConfig?.passwordPolicy ?? defaultPolicy)),
    defaultValues: {
      email: inviteEmail || '',
      password: '',
      confirmPassword: '',
    },
  });

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
    mutationFn: async (values: RegisterValues) => {
      const response = await api.post('/auth/register', {
        email: values.email,
        password: values.password,
        inviteToken
      });
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
      handleApiError(error, form.setError, 'Failed to create account. Please check your details.');
    }
  });

  const onSubmit = (values: RegisterValues) => {
    registerMutation.mutate(values);
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
            <div className="h-14 w-14 rounded-xl flex items-center justify-center bg-zinc-950 border border-border/40 shadow-sm transition-shadow">
              <ToggleMeshIcon className="h-8 w-8 text-zinc-300 transition-colors duration-300" />
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
                We've sent a confirmation link to <strong>{form.getValues().email}</strong>. Please click the link to verify your account before logging in.
              </p>
              <Button className="w-full mt-4" asChild>
                <Link to="/login">Go to Login</Link>
              </Button>
            </div>
          ) : systemConfig?.allowOpenRegistration === false && !inviteToken ? (
            <div className="text-center space-y-4 py-6">
              <div className="bg-muted text-muted-foreground p-4 rounded-full inline-block mb-2">
                <Shield className="h-8 w-8" />
              </div>
              <h3 className="text-xl font-medium">Invite Only</h3>
              <p className="text-muted-foreground text-sm max-w-sm mx-auto">
                Open registration is currently disabled. You must have an invitation link to create an account.
              </p>
            </div>
          ) : (
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
                          disabled={registerMutation.isPending || !!inviteEmail}
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
                      <FormLabel>Password</FormLabel>
                      <FormControl>
                        <Input
                          type="password"
                          placeholder="••••••••"
                          {...field}
                          className="bg-muted/50 border-border/50 focus-visible:ring-1"
                          disabled={registerMutation.isPending}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name="confirmPassword"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Confirm Password</FormLabel>
                      <FormControl>
                        <Input
                          type="password"
                          placeholder="••••••••"
                          {...field}
                          className="bg-muted/50 border-border/50 focus-visible:ring-1"
                          disabled={registerMutation.isPending}
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

                <Button
                  type="submit"
                  className="w-full h-11 text-primary-foreground font-medium mt-4"
                  disabled={registerMutation.isPending}
                >
                  {registerMutation.isPending ? 'Creating account...' : 'Create account'}
                </Button>
              </form>
            </Form>
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