import { useSystemConfig } from '@/api/queries';
import { Alert, AlertTitle, AlertDescription } from '@/components/ui/alert';
import { AlertTriangle } from 'lucide-react';

interface AnalyticsDisabledBannerProps {
    title?: string;
    description?: string;
    className?: string;
}

export function AnalyticsDisabledBanner({
    title = "Analytics Engine Disabled",
    description = "Telemetry ingestion and conversion tracking are currently turned off (TM_ENABLE_ANALYTICS=false). Feature flag evaluations and targeting rules function normally, but traffic graphs and A/B insights are inactive.",
    className = ""
}: AnalyticsDisabledBannerProps) {
    const { data: sysConfig } = useSystemConfig();

    if (sysConfig?.analyticsEnabled !== false) return null;

    return (
        <Alert className={`border-amber-500/30 bg-amber-500/10 text-amber-200 py-3 ${className}`}>
            <AlertTriangle className="h-4 w-4 text-amber-400 shrink-0" />
            <div>
                <AlertTitle className="font-semibold text-amber-300 text-xs">{title}</AlertTitle>
                <AlertDescription className="text-xs text-amber-200/80 mt-0.5 leading-relaxed">
                    {description}
                </AlertDescription>
            </div>
        </Alert>
    );
}
