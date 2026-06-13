import { type LucideIcon } from 'lucide-react';
import { type ReactNode } from 'react';

interface EmptyStateProps {
    icon: LucideIcon;
    title: string;
    description: string;
    action?: ReactNode;
}

export function EmptyState({ icon: Icon, title, description, action }: EmptyStateProps) {
    return (
        <div className="flex flex-col items-center justify-center p-8 text-center border-border/40 bg-zinc-950/20 h-full min-h-[250px]">
            <div className="h-12 w-12 rounded-full bg-zinc-900/50 flex items-center justify-center mb-4 ring-1 ring-white/5">
                <Icon className="h-6 w-6 text-muted-foreground" />
            </div>
            <h3 className="font-semibold text-lg text-zinc-200 tracking-tight">{title}</h3>
            <p className="text-muted-foreground text-sm mb-6 max-w-sm mt-1">{description}</p>
            {action}
        </div>
    );
}