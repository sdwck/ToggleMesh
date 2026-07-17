import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ShieldAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';

export function RequireTwoFactorOverlay() {
    const [isVisible, setIsVisible] = useState(false);
    const navigate = useNavigate();

    useEffect(() => {
        const handleTwoFactorRequired = () => {
            setIsVisible(true);
        };

        window.addEventListener('togglemesh:requires-twofactor', handleTwoFactorRequired);

        return () => {
            window.removeEventListener('togglemesh:requires-twofactor', handleTwoFactorRequired);
        };
    }, []);

    if (!isVisible) return null;

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-background/80 backdrop-blur-sm">
            <div className="flex flex-col items-center justify-center max-w-md p-8 space-y-6 text-center border shadow-lg bg-card rounded-xl border-border/40">
                <div className="flex items-center justify-center w-16 h-16 rounded-full bg-destructive/10">
                    <ShieldAlert className="w-8 h-8 text-destructive" />
                </div>
                <div className="space-y-2">
                    <h2 className="text-2xl font-bold tracking-tight text-foreground">Action Required</h2>
                    <p className="text-sm text-muted-foreground">
                        This organization requires Two-Factor Authentication (2FA) to access its resources.
                        Please enable 2FA on your personal account to continue.
                    </p>
                </div>
                <div className="flex flex-col w-full gap-2 pt-4">
                    <Button 
                        className="w-full" 
                        onClick={() => {
                            setIsVisible(false);
                            navigate('/settings');
                        }}
                    >
                        Go to Account Settings
                    </Button>
                    <Button 
                        variant="ghost" 
                        className="w-full"
                        onClick={() => {
                            setIsVisible(false);
                            navigate('/');
                        }}
                    >
                        Return Home
                    </Button>
                </div>
            </div>
        </div>
    );
}
