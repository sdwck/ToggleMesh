import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { ArrowLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ToggleMeshIcon } from '@/components/icons/ToggleMeshIcon';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
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

const forgotPasswordSchema = z.object({
  email: z.string().min(1, 'Email is required').email('Invalid email format'),
});

type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>;

export function ForgotPasswordPage() {
  const navigate = useNavigate();
  const [submitted, setSubmitted] = useState(false);

  const form = useForm<ForgotPasswordValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: {
      email: '',
    },
  });

  const forgotPasswordMutation = useMutation({
    mutationFn: async (values: ForgotPasswordValues) => {
      const response = await api.post('/auth/forgot-password', values);
      return response.data;
    },
    onSuccess: () => {
      setSubmitted(true);
      toast.success('Password reset instructions sent to your email.');
    },
    onError: (error: any) => {
      if (error.response?.status === 400) {
        handleApiError(error, form.setError, 'Validation failed');
      } else {
        setSubmitted(true);
        toast.success('Password reset instructions sent to your email.');
      }
    }
  });

  const onSubmit = (values: ForgotPasswordValues) => {
    forgotPasswordMutation.mutate(values);
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
          <CardTitle className="text-2xl font-bold tracking-tight">Forgot Password</CardTitle>
          <CardDescription className="text-muted-foreground">
            {submitted
              ? "If an account exists with that email, we've sent you instructions to reset your password."
              : "Enter your email address and we'll send you a link to reset your password."}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {!submitted ? (
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
                          disabled={forgotPasswordMutation.isPending}
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
                  disabled={forgotPasswordMutation.isPending}
                >
                  {forgotPasswordMutation.isPending ? 'Sending...' : 'Send Reset Link'}
                </Button>
              </form>
            </Form>
          ) : (
            <div className="space-y-4 text-center">
              <p className="text-sm text-muted-foreground bg-muted/20 p-4 rounded-md border border-border/40">
                Please check your inbox at <strong>{form.getValues().email}</strong> for the reset link.
              </p>
            </div>
          )}
        </CardContent>
        <CardFooter className="flex justify-center border-t border-border/40 pt-6">
          <Button
            variant="ghost"
            className="text-muted-foreground hover:text-foreground flex items-center gap-2"
            onClick={() => navigate('/login')}
          >
            <ArrowLeft className="h-4 w-4" /> Back to login
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
}
