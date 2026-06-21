import { useEffect, useState } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { Shield, CheckCircle, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import api from '@/api/axios';

export function ConfirmEmailPage() {
    const [searchParams] = useSearchParams();
    const userId = searchParams.get('userId');
    const token = searchParams.get('token');
    const [status, setStatus] = useState<'loading' | 'success' | 'error'>('loading');

    useEffect(() => {
        if (!userId || !token) {
            setStatus('error');
            return;
        }

        api.post('/auth/confirm-email', { userId, token })
            .then(() => setStatus('success'))
            .catch(() => setStatus('error'));
    }, [userId, token]);

    return (
        <div className="min-h-screen flex items-center justify-center bg-background p-4">
            <Card className="w-full max-w-md border-border/40 shadow-2xl text-center">
                <CardHeader className="space-y-2 pb-8">
                    <div className="flex justify-center mb-4">
                        <div className={`h-12 w-12 rounded-xl flex items-center justify-center ${status === 'success' ? 'bg-green-500/10' : status === 'error' ? 'bg-red-500/10' : 'bg-primary/10'}`}>
                            {status === 'loading' && <Shield className="h-6 w-6 text-primary animate-pulse" />}
                            {status === 'success' && <CheckCircle className="h-6 w-6 text-green-500" />}
                            {status === 'error' && <XCircle className="h-6 w-6 text-red-500" />}
                        </div>
                    </div>
                    <CardTitle className="text-2xl font-bold tracking-tight">
                        {status === 'loading' && 'Confirming Email...'}
                        {status === 'success' && 'Email Confirmed!'}
                        {status === 'error' && 'Confirmation Failed'}
                    </CardTitle>
                    <CardDescription className="text-muted-foreground">
                        {status === 'loading' && 'Please wait while we verify your email address.'}
                        {status === 'success' && 'Your email address has been successfully verified.'}
                        {status === 'error' && 'The confirmation link may be invalid or expired.'}
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    {(status === 'success' || status === 'error') && (
                        <Button className="w-full" asChild>
                            <Link to="/login">Go to Login</Link>
                        </Button>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
