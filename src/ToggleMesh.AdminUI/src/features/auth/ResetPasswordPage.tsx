import { useState, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useSystemConfig } from '@/api/queries';
import { useMutation } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ToggleMeshIcon } from '@/components/icons/ToggleMeshIcon';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { toast } from 'sonner';
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
import { handleApiError } from '@/api/errorUtils';

const getResetPasswordSchema = (policy: { minimumLength: number, requireDigit: boolean, requireLowercase: boolean, requireUppercase: boolean, requireNonAlphanumeric: boolean }) => {
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
    password: passwordSchema,
    confirmPassword: z.string(),
  }).refine((data) => data.password === data.confirmPassword, {
    message: "Passwords do not match",
    path: ["confirmPassword"],
  });
};

type ResetPasswordValues = z.infer<ReturnType<typeof getResetPasswordSchema>>;

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [submitted, setSubmitted] = useState(false);
  
  const token = searchParams.get('token');
  const email = searchParams.get('email');

  const { data: systemConfig } = useSystemConfig();
  const defaultPolicy = { minimumLength: 8, requireDigit: true, requireLowercase: true, requireUppercase: true, requireNonAlphanumeric: true };

  const form = useForm<ResetPasswordValues>({
    resolver: zodResolver(getResetPasswordSchema(systemConfig?.passwordPolicy ?? defaultPolicy)),
    defaultValues: {
      password: '',
      confirmPassword: '',
    },
  });

  useEffect(() => {
    if (!token || !email) {
      toast.error("Invalid reset link.");
      navigate('/login');
    }
  }, [token, email, navigate]);

  const resetPasswordMutation = useMutation({
    mutationFn: async (values: ResetPasswordValues) => {
      const response = await api.post('/auth/reset-password', { email, token, newPassword: values.password });
      return response.data;
    },
    onSuccess: () => {
      setSubmitted(true);
      toast.success('Password has been reset successfully.');
    },
    onError: (error: any) => {
      handleApiError(error, form.setError, 'Failed to reset password. The link might be expired.');
    }
  });

  const onSubmit = (values: ResetPasswordValues) => {
    resetPasswordMutation.mutate(values);
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-background p-4">
      <Card className="w-full max-w-md border-border/40 shadow-2xl">
        <CardHeader className="space-y-2 text-center pb-8">
          <div className="flex justify-center mb-4">
            <div className="h-14 w-14 rounded-xl flex items-center justify-center bg-zinc-950 border border-border/40 shadow-sm transition-shadow">
              <ToggleMeshIcon className="h-8 w-8 text-zinc-300 transition-colors duration-300" />
            </div>
          </div>
          <CardTitle className="text-2xl font-bold tracking-tight">Set New Password</CardTitle>
          <CardDescription className="text-muted-foreground">
            {submitted 
              ? "Your password has been successfully updated."
              : `Set a new password for ${email}`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {!submitted ? (
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                <FormField
                  control={form.control}
                  name="password"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>New Password</FormLabel>
                      <FormControl>
                        <Input
                          type="password"
                          {...field}
                          className="bg-muted/50 border-border/50 focus-visible:ring-1"
                          disabled={resetPasswordMutation.isPending}
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
                      <FormLabel>Confirm New Password</FormLabel>
                      <FormControl>
                        <Input
                          type="password"
                          {...field}
                          className="bg-muted/50 border-border/50 focus-visible:ring-1"
                          disabled={resetPasswordMutation.isPending}
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
                  disabled={resetPasswordMutation.isPending}
                >
                  {resetPasswordMutation.isPending ? 'Resetting...' : 'Reset Password'}
                </Button>
              </form>
            </Form>
          ) : (
            <div className="space-y-4 text-center">
              <p className="text-sm text-muted-foreground bg-muted/20 p-4 rounded-md border border-border/40">
                You can now log in with your new password.
              </p>
              <Button
                className="w-full h-11 text-primary-foreground font-medium mt-4"
                onClick={() => navigate('/login')}
              >
                Go to Login
              </Button>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
