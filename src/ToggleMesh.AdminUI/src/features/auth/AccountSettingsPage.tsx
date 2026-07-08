import { ProfileSettingsCard } from './components/ProfileSettingsCard';
import { SecuritySettingsCard } from './components/SecuritySettingsCard';
import { PersonalAccessTokensCard } from './components/PersonalAccessTokensCard';

export function AccountSettingsPage() {
    return (
        <div className="space-y-6">
            <div>
                <h2 className="text-2xl font-bold tracking-tight">Account Settings</h2>
                <p className="text-muted-foreground">Manage your profile details and developer access tokens.</p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 items-start">
                <ProfileSettingsCard />
                <SecuritySettingsCard />
            </div>

            <PersonalAccessTokensCard />
        </div>
    );
}