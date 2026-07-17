import { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

import {
    InputOTP,
    InputOTPGroup,
    InputOTPSeparator,
    InputOTPSlot,
} from "@/components/ui/input-otp";
import { Input } from '@/components/ui/input';
import { ShieldAlert, ShieldCheck } from 'lucide-react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { toast } from 'sonner';
import api from '@/api/axios';
import { toastApiError } from '@/api/errorUtils';
import { useUserProfile } from '@/api/queries';
import { useQueryClient } from '@tanstack/react-query';
import { QRCodeSVG } from 'qrcode.react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';

interface SetupResponse {
    sharedKey: string;
    authenticatorUri: string;
}

interface EnableResponse {
    recoveryCodes: string[];
}

export function TwoFactorSettingsCard() {
    const { data: profile } = useUserProfile();
    const queryClient = useQueryClient();

    const [isEnableOpen, setIsEnableOpen] = useState(false);
    const [isDisableOpen, setIsDisableOpen] = useState(false);
    const [isRegenerateOpen, setIsRegenerateOpen] = useState(false);
    const [isUsingRecoveryCode, setIsUsingRecoveryCode] = useState(false);

    const [setupData, setSetupData] = useState<SetupResponse | null>(null);
    const [verificationCode, setVerificationCode] = useState('');
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);
    const [canCloseRecoveryCodes, setCanCloseRecoveryCodes] = useState(false);

    const handleOpenEnable = async () => {
        setIsEnableOpen(true);
        setVerificationCode('');
        setRecoveryCodes([]);
        try {
            const res = await api.get<SetupResponse>('/auth/2fa/setup');
            setSetupData(res.data);
        } catch (error) {
            toast.error('Failed to load 2FA setup data');
            setIsEnableOpen(false);
        }
    };

    const handleEnableSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setIsSubmitting(true);
        try {
            const res = await api.post<EnableResponse>('/auth/2fa/enable', { code: verificationCode });
            setRecoveryCodes(res.data.recoveryCodes);
            setCanCloseRecoveryCodes(false);
            setTimeout(() => setCanCloseRecoveryCodes(true), 3000);
            toast.success('Two-factor authentication enabled successfully!');
        } catch (error: any) {
            toastApiError(error, 'Invalid verification code');
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleDisableSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setIsSubmitting(true);
        try {
            await api.post('/auth/2fa/disable', { code: verificationCode });
            toast.success('Two-factor authentication disabled');
            setIsDisableOpen(false);
            setVerificationCode('');
            queryClient.invalidateQueries({ queryKey: ['user', 'profile'] });
        } catch (error: any) {
            toastApiError(error, 'Invalid verification code');
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleRegenerateSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setIsSubmitting(true);
        try {
            const res = await api.post<{ recoveryCodes: string[] }>('/auth/2fa/recovery-codes', { code: verificationCode });
            setRecoveryCodes(res.data.recoveryCodes);
            setCanCloseRecoveryCodes(false);
            setTimeout(() => setCanCloseRecoveryCodes(true), 3000);
            toast.success('Recovery codes regenerated successfully!');
            setIsRegenerateOpen(false);
            setIsEnableOpen(true);
            setVerificationCode('');
        } catch (error: any) {
            toastApiError(error, 'Invalid verification code');
        } finally {
            setIsSubmitting(false);
        }
    };

    if (!profile) return null;

    return (
        <Card className="border-border/40 bg-zinc-950/20 h-full flex flex-col">
            <CardHeader>
                <CardTitle className="text-xl flex items-center gap-2">
                    {profile.twoFactorEnabled ? (
                        <ShieldCheck className="w-5 h-5 text-emerald-500" />
                    ) : (
                        <ShieldAlert className="w-5 h-5 text-muted-foreground" />
                    )}
                    Two-Factor Authentication
                </CardTitle>
                <CardDescription>
                    Add an extra layer of security to your account using an authenticator app.
                </CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col flex-1">
                <div className="flex flex-col gap-6 h-full">
                    {profile.twoFactorEnabled ? (
                        <>
                            <p className="text-sm text-muted-foreground">
                                Your account is secured with two-factor authentication.
                            </p>
                            <div className="mt-auto w-full pt-8 border-t border-border/40 space-y-6">
                                <div>
                                    <div className="flex items-center justify-between mb-2">
                                        <h4 className="text-sm font-medium">Recovery Codes</h4>
                                        {profile.recoveryCodesLeft !== undefined && (
                                            <span className={`text-xs px-2 py-0.5 rounded-full ${profile.recoveryCodesLeft === 0 ? 'bg-destructive/10 text-destructive' : profile.recoveryCodesLeft <= 3 ? 'bg-orange-500/10 text-orange-500' : 'bg-muted text-muted-foreground'}`}>
                                                {profile.recoveryCodesLeft} remaining
                                            </span>
                                        )}
                                    </div>
                                    <div className="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between">
                                        <div className="space-y-1">
                                            <p className="text-sm text-muted-foreground">
                                                Generate new recovery codes. This will invalidate your existing codes.
                                            </p>
                                            {profile.recoveryCodesLeft === 0 && (
                                                <p className="text-xs text-destructive font-medium">
                                                    You have 0 recovery codes remaining. You must regenerate them to avoid losing access to your account.
                                                </p>
                                            )}
                                        </div>
                                        <Button variant={profile.recoveryCodesLeft === 0 ? "default" : "outline"} onClick={() => { setVerificationCode(''); setIsRegenerateOpen(true); setIsUsingRecoveryCode(false); }} className={`shrink-0 ${profile.recoveryCodesLeft === 0 ? 'bg-destructive text-destructive-foreground hover:bg-destructive/90' : ''}`}>
                                            Regenerate Codes
                                        </Button>
                                    </div>
                                </div>

                                <div>
                                    <h4 className="text-sm font-medium text-destructive mb-2">Danger Zone</h4>
                                    <div className="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between">
                                        <p className="text-sm text-muted-foreground">
                                            Disabling 2FA will remove the extra layer of security from your account.
                                        </p>
                                        <Button variant="outline" onClick={() => { setVerificationCode(''); setIsDisableOpen(true); setIsUsingRecoveryCode(false); }} className="bg-destructive/10 text-destructive border-transparent hover:bg-destructive/20 hover:text-destructive shrink-0">Disable 2FA</Button>
                                    </div>
                                </div>
                            </div>
                        </>
                    ) : (
                        <>
                            <p className="text-sm text-muted-foreground">
                                Scan a QR code using an app like Google Authenticator to secure your account.
                            </p>
                            <Button onClick={handleOpenEnable} className="mt-auto">Enable 2FA</Button>
                        </>
                    )}
                </div>
            </CardContent>

            <Dialog open={isRegenerateOpen} onOpenChange={setIsRegenerateOpen}>
                <DialogContent className="sm:max-w-[425px]" onInteractOutside={(e) => e.preventDefault()}>
                    <DialogHeader>
                        <DialogTitle>Regenerate Recovery Codes</DialogTitle>
                        <DialogDescription>
                            Please enter a code from your authenticator app to verify your identity.
                        </DialogDescription>
                    </DialogHeader>
                    <form onSubmit={handleRegenerateSubmit} className="space-y-4">
                        <div className="space-y-2 flex flex-col items-center w-full">
                            <label className="text-sm font-medium">
                                {isUsingRecoveryCode ? 'Recovery Code' : 'Authenticator Code'}
                            </label>
                            {isUsingRecoveryCode ? (
                                <Input
                                    value={verificationCode}
                                    onChange={(e: React.ChangeEvent<HTMLInputElement>) => setVerificationCode(e.target.value)}
                                    disabled={isSubmitting}
                                    placeholder="e.g. XXXXX-XXXXX"
                                    className="text-center tracking-widest uppercase font-mono max-w-[250px]"
                                    autoFocus
                                />
                            ) : (
                                <InputOTP maxLength={6} value={verificationCode} onChange={setVerificationCode}>
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
                        <div className="flex justify-end pt-2">
                            <Button
                                type="button"
                                variant="ghost"
                                className="text-xs text-muted-foreground h-auto p-0"
                                onClick={() => { setIsUsingRecoveryCode(!isUsingRecoveryCode); setVerificationCode(''); }}
                            >
                                {isUsingRecoveryCode ? 'Use authenticator app' : 'Use recovery code instead'}
                            </Button>
                        </div>
                        <DialogFooter>
                            <Button type="button" variant="outline" onClick={() => setIsRegenerateOpen(false)}>Cancel</Button>
                            <Button type="submit" disabled={isSubmitting || (isUsingRecoveryCode ? verificationCode.length < 10 : verificationCode.length < 6)}>
                                {isSubmitting ? 'Verifying...' : 'Regenerate'}
                            </Button>
                        </DialogFooter>
                    </form>
                </DialogContent>
            </Dialog>

            <Dialog open={isDisableOpen} onOpenChange={setIsDisableOpen}>
                <DialogContent className="sm:max-w-[425px]" onInteractOutside={(e) => e.preventDefault()}>
                    <DialogHeader>
                        <DialogTitle>Disable Two-Factor Authentication</DialogTitle>
                        <DialogDescription>
                            To disable 2FA, please enter a code from your authenticator app to verify your identity.
                        </DialogDescription>
                    </DialogHeader>
                    <form onSubmit={handleDisableSubmit} className="space-y-4">
                        <div className="space-y-2 flex flex-col items-center w-full">
                            <label className="text-sm font-medium">
                                {isUsingRecoveryCode ? 'Recovery Code' : 'Authenticator Code'}
                            </label>
                            {isUsingRecoveryCode ? (
                                <Input
                                    value={verificationCode}
                                    onChange={(e: React.ChangeEvent<HTMLInputElement>) => setVerificationCode(e.target.value)}
                                    disabled={isSubmitting}
                                    placeholder="e.g. XXXXX-XXXXX"
                                    className="text-center tracking-widest uppercase font-mono max-w-[250px]"
                                    autoFocus
                                />
                            ) : (
                                <InputOTP maxLength={6} value={verificationCode} onChange={setVerificationCode}>
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
                        <div className="flex justify-end pt-2">
                            <Button
                                type="button"
                                variant="ghost"
                                className="text-xs text-muted-foreground h-auto p-0"
                                onClick={() => { setIsUsingRecoveryCode(!isUsingRecoveryCode); setVerificationCode(''); }}
                            >
                                {isUsingRecoveryCode ? 'Use authenticator app' : 'Use recovery code instead'}
                            </Button>
                        </div>
                        <DialogFooter>
                            <Button type="button" variant="outline" onClick={() => setIsDisableOpen(false)}>Cancel</Button>
                            <Button type="submit" variant="destructive" disabled={isSubmitting || (isUsingRecoveryCode ? verificationCode.length < 10 : verificationCode.length < 6)}>
                                {isSubmitting ? 'Disabling...' : 'Disable'}
                            </Button>
                        </DialogFooter>
                    </form>
                </DialogContent>
            </Dialog>

            <Dialog open={isEnableOpen} onOpenChange={(open) => {
                if (!open && recoveryCodes.length > 0 && !canCloseRecoveryCodes) return;
                setIsEnableOpen(open);
                if (!open) {
                    setRecoveryCodes([]);
                    queryClient.invalidateQueries({ queryKey: ['user', 'profile'] });
                }
            }}>
                <DialogContent
                    className="sm:max-w-[500px]"
                    hideCloseButton={recoveryCodes.length > 0}
                    onInteractOutside={(e) => e.preventDefault()}
                    onKeyDown={(e) => {
                        if (e.key === 'Enter') e.preventDefault();
                    }}
                >
                    <DialogHeader>
                        <DialogTitle>Setup Two-Factor Authentication</DialogTitle>
                    </DialogHeader>

                    {recoveryCodes.length > 0 ? (
                        <div className="space-y-4">
                            <Alert className="bg-emerald-500/10 text-emerald-500 border-emerald-500/20">
                                <ShieldCheck className="h-4 w-4" />
                                <AlertTitle>Success!</AlertTitle>
                                <AlertDescription>
                                    Two-factor authentication has been enabled.
                                </AlertDescription>
                            </Alert>

                            <div className="space-y-2">
                                <h4 className="text-sm font-medium">Save these recovery codes</h4>
                                <p className="text-xs text-muted-foreground">
                                    If you lose access to your authenticator device, you can use these backup codes to sign in.
                                    Keep them somewhere safe. They will only be shown once.
                                </p>
                                <div className="bg-muted p-4 rounded-md grid grid-cols-2 gap-2 font-mono text-sm mt-2">
                                    {recoveryCodes.map(code => (
                                        <div key={code}>{code}</div>
                                    ))}
                                </div>
                            </div>

                            <DialogFooter>
                                <Button
                                    type="button"
                                    disabled={!canCloseRecoveryCodes}
                                    onClick={() => {
                                        setIsEnableOpen(false);
                                        setRecoveryCodes([]);
                                        queryClient.invalidateQueries({ queryKey: ['user', 'profile'] });
                                    }}
                                >
                                    {canCloseRecoveryCodes ? 'I have saved my codes' : 'Please save your codes...'}
                                </Button>
                            </DialogFooter>
                        </div>
                    ) : (
                        <div className="space-y-6">
                            {setupData ? (
                                <div className="flex flex-col items-center justify-center space-y-4">
                                    <div className="bg-white p-4 rounded-xl shadow-sm">
                                        <QRCodeSVG value={setupData.authenticatorUri} size={200} />
                                    </div>
                                    <div className="text-center space-y-1">
                                        <p className="text-sm font-medium">Can't scan the QR code?</p>
                                        <p className="text-xs text-muted-foreground font-mono bg-muted p-2 rounded">
                                            {setupData.sharedKey}
                                        </p>
                                    </div>
                                </div>
                            ) : (
                                <div className="flex justify-center py-8">
                                    <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
                                </div>
                            )}

                            <form onSubmit={handleEnableSubmit} className="space-y-4">
                                <div className="space-y-2 flex flex-col items-center w-full">
                                    <label className="text-sm font-medium">Enter the 6-digit code</label>
                                    <InputOTP maxLength={6} value={verificationCode} onChange={setVerificationCode}>
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
                                </div>
                                <DialogFooter>
                                    <Button type="button" variant="outline" onClick={() => setIsEnableOpen(false)}>Cancel</Button>
                                    <Button type="submit" disabled={isSubmitting || verificationCode.length < 6 || !setupData}>
                                        {isSubmitting ? 'Verifying...' : 'Verify and Enable'}
                                    </Button>
                                </DialogFooter>
                            </form>
                        </div>
                    )}
                </DialogContent>
            </Dialog>
        </Card>
    );
}
