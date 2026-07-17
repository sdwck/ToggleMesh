import { ProfileSettingsCard } from './components/ProfileSettingsCard';
import { SecuritySettingsCard } from './components/SecuritySettingsCard';
import { TwoFactorSettingsCard } from './components/TwoFactorSettingsCard';
import { PersonalAccessTokensCard } from './components/PersonalAccessTokensCard';

export function AccountSettingsPage() {
    return (
        <div className="space-y-6">
            <div>
                <h2 className="text-2xl font-bold tracking-tight">Account Settings</h2>
                <p className="text-muted-foreground">Manage your profile details and developer access tokens.</p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="flex flex-col gap-4">
                    <ProfileSettingsCard />
                </div>
                <TwoFactorSettingsCard />
            </div>

            <SecuritySettingsCard />

            <PersonalAccessTokensCard />
        </div>
    );
}