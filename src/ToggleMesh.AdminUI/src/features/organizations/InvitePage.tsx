import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Building2, CheckCircle, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { toast } from 'sonner';
import { useQueryClient } from '@tanstack/react-query';
import api from '@/api/axios';
import type { OrganizationInvitationDto } from '@/api/types';
import { useOrganizationStore } from '@/stores/useOrganizationStore';

export function InvitePage() {
    const { token } = useParams<{ token: string }>();
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const { setActiveOrganizationId } = useOrganizationStore();
    const [status, setStatus] = useState<'loading' | 'pending' | 'success' | 'error'>('loading');
    const [invite, setInvite] = useState<OrganizationInvitationDto | null>(null);

    useEffect(() => {
        if (!token) {
            setStatus('error');
            return;
        }

        api.get<OrganizationInvitationDto>(`/organizations/invites/${token}`)
            .then(({ data }) => {
                setInvite(data);
                setStatus('pending');
            })
            .catch(() => setStatus('error'));
    }, [token]);

    const handleAccept = async () => {
        if (!localStorage.getItem('accessToken')) {
            localStorage.setItem('pendingInviteToken', token || '');
            navigate(`/login?inviteToken=${token}`);
            return;
        }

        try {
            const { data } = await api.post<{ organizationId: string }>(`/organizations/invites/${token}/accept`, {});
            setActiveOrganizationId(data.organizationId);
            queryClient.invalidateQueries({ queryKey: ['organizations'] });
            queryClient.invalidateQueries({ queryKey: ['projects'] });
            
            setStatus('success');
            toast.success('Invitation accepted!');
            setTimeout(() => navigate('/projects'), 2000);
        } catch (error: any) {
            if (error.response?.status === 401) {
                localStorage.setItem('pendingInviteToken', token || '');
                navigate(`/login?inviteToken=${token}`);
            } else {
                toast.error(error.response?.data?.message || 'Failed to accept invitation');
            }
        }
    };

    const isAuthenticated = !!localStorage.getItem('accessToken');

    return (
        <div className="min-h-screen flex items-center justify-center bg-background p-4">
            <Card className="w-full max-w-md border-border/40 shadow-2xl text-center">
                <CardHeader className="space-y-2 pb-8">
                    <div className="flex justify-center mb-4">
                        <div className={`h-12 w-12 rounded-xl flex items-center justify-center ${status === 'success' ? 'bg-green-500/10' : status === 'error' ? 'bg-red-500/10' : 'bg-primary/10'}`}>
                            {status === 'loading' && <Building2 className="h-6 w-6 text-primary animate-pulse" />}
                            {status === 'pending' && <Building2 className="h-6 w-6 text-primary" />}
                            {status === 'success' && <CheckCircle className="h-6 w-6 text-green-500" />}
                            {status === 'error' && <XCircle className="h-6 w-6 text-red-500" />}
                        </div>
                    </div>
                    <CardTitle className="text-2xl font-bold tracking-tight">
                        {status === 'loading' && 'Loading Invitation...'}
                        {status === 'pending' && `Join ${invite?.organizationName}`}
                        {status === 'success' && 'Welcome!'}
                        {status === 'error' && 'Invalid Invitation'}
                    </CardTitle>
                    <CardDescription className="text-muted-foreground">
                        {status === 'loading' && 'Please wait while we fetch the details.'}
                        {status === 'pending' && `You have been invited to join as a ${invite?.role === 1 ? 'Admin' : 'Member'}.`}
                        {status === 'success' && 'You have successfully joined the organization. Redirecting...'}
                        {status === 'error' && 'The invitation link may be invalid, expired, or you are not logged in with the correct email address.'}
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    {status === 'pending' && (
                        <div className="space-y-3">
                            {isAuthenticated ? (
                                <>
                                    <Button className="w-full" onClick={handleAccept}>
                                        Accept Invitation
                                    </Button>
                                    <Button className="w-full" variant="outline" onClick={() => navigate('/projects')}>
                                        Cancel
                                    </Button>
                                </>
                            ) : (
                                <>
                                    <Button className="w-full" onClick={() => {
                                        localStorage.setItem('pendingInviteToken', token || '');
                                        navigate(`/register?inviteToken=${token}&email=${encodeURIComponent(invite?.email || '')}`);
                                    }}>
                                        Create Account to Join
                                    </Button>
                                    <Button className="w-full" variant="outline" onClick={() => {
                                        localStorage.setItem('pendingInviteToken', token || '');
                                        navigate(`/login?inviteToken=${token}`);
                                    }}>
                                        Log in to Existing Account
                                    </Button>
                                </>
                            )}
                        </div>
                    )}
                    {status === 'error' && (
                        <Button className="w-full" onClick={() => navigate('/projects')}>
                            Go to Dashboard
                        </Button>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
