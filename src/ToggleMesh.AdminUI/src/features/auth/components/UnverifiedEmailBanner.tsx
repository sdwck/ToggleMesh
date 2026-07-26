import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { AlertTriangle, Mail, CheckCircle2, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useUserProfile, useSystemConfig } from '@/api/queries';
import { EmailVerificationMethod } from '@/api/types';
import api from '@/api/axios';
import { toastApiError } from '@/api/errorUtils';

export function UnverifiedEmailBanner() {
    const { data: profile } = useUserProfile();
    const { data: systemConfig } = useSystemConfig();
    const [isDismissed, setIsDismissed] = useState(false);
    const [isSuccess, setIsSuccess] = useState(false);

    const resendMutation = useMutation({
        mutationFn: async () => {
            if (!profile?.email) return;
            await api.post('/auth/resend-confirmation', { email: profile.email });
        },
        onSuccess: () => {
            setIsSuccess(true);
        },
        onError: (error) => {
            toastApiError(error, 'Failed to send verification email');
        }
    });

    const needsVerification = profile?.emailVerificationMethod === EmailVerificationMethod.SkippedNoSmtp && systemConfig?.enableEmails === true;

    if (!needsVerification || isDismissed) {
        return null;
    }

    return (
        <div className="bg-yellow-500/10 border-b border-yellow-500/20 px-6 py-3 flex items-center justify-between shadow-sm relative z-50">
            <div className="flex items-center gap-3 text-yellow-600/90 dark:text-yellow-500/90 max-w-[80%]">
                {isSuccess ? (
                    <CheckCircle2 className="h-5 w-5 shrink-0 text-emerald-500" />
                ) : (
                    <AlertTriangle className="h-5 w-5 shrink-0" />
                )}

                <div className="flex flex-col sm:flex-row sm:items-center gap-1 sm:gap-4">
                    <p className="text-sm font-medium">
                        {isSuccess
                            ? "Verification email sent! Please check your inbox and click the link to secure your account."
                            : "Your email address is unverified. Please verify your email to enhance account security."
                        }
                    </p>

                    {!isSuccess && (
                        <Button
                            variant="outline"
                            size="sm"
                            className="h-7 text-xs bg-yellow-500/10 hover:bg-yellow-500/20 border-yellow-500/30 text-yellow-700 dark:text-yellow-400 font-medium px-3 shrink-0"
                            onClick={() => resendMutation.mutate()}
                            disabled={resendMutation.isPending}
                        >
                            {resendMutation.isPending ? (
                                <span className="flex items-center gap-2">
                                    <span className="h-3 w-3 rounded-full border-2 border-current border-t-transparent animate-spin" />
                                    Sending...
                                </span>
                            ) : (
                                <span className="flex items-center gap-2">
                                    <Mail className="h-3 w-3" />
                                    Send Verification Email
                                </span>
                            )}
                        </Button>
                    )}
                </div>
            </div>

            <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8 text-yellow-600/70 hover:text-yellow-700 dark:text-yellow-500/70 dark:hover:text-yellow-400 hover:bg-yellow-500/10 rounded-full shrink-0"
                onClick={() => setIsDismissed(true)}
            >
                <X className="h-4 w-4" />
            </Button>
        </div>
    );
}
