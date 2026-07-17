import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import api from '@/api/axios';
import { Card } from '@/components/ui/card';
import { FolderGit2, ArrowRight, TerminalSquare, FlaskConical, Sparkles, AlertTriangle } from 'lucide-react';
import type { Project } from '@/api/types';

interface ProjectCardProps {
    project: Project;
}

const formatEvals = (n: number): string => {
    if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
    if (n >= 1_000) return `${(n / 1_000).toFixed(1)}k`;
    return String(n);
};

const roleLabel = (role: number): string => {
    switch (role) {
        case 0: return 'Owner';
        case 1: return 'Admin';
        case 2: return 'Editor';
        case 3: return 'Viewer';
        default: return 'Member';
    }
};

export function ProjectCard({ project }: ProjectCardProps) {
    const navigate = useNavigate();
    const queryClient = useQueryClient();

    const handlePrefetchProject = (projId: string) => {
        queryClient.prefetchQuery({
            queryKey: ['projects', projId],
            queryFn: async () => {
                const { data } = await api.get(`/projects/${projId}`, { headers: { 'X-Skip-TwoFactor-Interceptor': 'true' } });
                return data;
            },
            staleTime: 5 * 60 * 1000,
        });
        queryClient.prefetchQuery({
            queryKey: ['projects', projId, 'flags', undefined, undefined],
            queryFn: async () => {
                const { data } = await api.get(`/projects/${projId}/flags`, { headers: { 'X-Skip-TwoFactor-Interceptor': 'true' } });
                return data;
            },
            staleTime: 5 * 60 * 1000,
        });
    };

    return (
        <Card
            className="flex flex-col border-border/40 hover:border-primary/40 bg-gradient-to-b from-zinc-950/40 to-zinc-950/10 hover:bg-zinc-900/40 transition-all duration-300 cursor-pointer group overflow-hidden relative shadow-sm hover:shadow-md"
            onClick={() => navigate(`/projects/${project.id}`)}
            onMouseEnter={() => handlePrefetchProject(project.id)}
        >
            <div className="p-5 flex-1 flex flex-col">
                <div className="flex items-start justify-between mb-5">
                    <div className="flex items-center gap-3 min-w-0">
                        <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-primary/20 to-primary/5 border border-primary/20 flex items-center justify-center text-primary shrink-0 shadow-sm group-hover:scale-105 transition-transform duration-300">
                            <FolderGit2 className="h-5 w-5" />
                        </div>
                        <div className="min-w-0">
                            <h3 className="font-semibold text-[15px] tracking-tight truncate group-hover:text-primary transition-colors">
                                {project.name}
                            </h3>
                            <span className="text-[12px] text-muted-foreground/70 font-medium block mt-0.5">
                                {roleLabel(project.userRole)}
                            </span>
                        </div>
                    </div>
                    <ArrowRight className="h-4 w-4 text-primary opacity-0 -translate-x-2 group-hover:opacity-100 group-hover:translate-x-0 transition-all duration-300 shrink-0 mt-2" />
                </div>

                {project.totalFlags === 0 && !project.evaluations24H ? (
                    <div className="flex-1 flex flex-col items-center justify-center text-muted-foreground/40 py-6 border border-dashed border-border/20 rounded-xl bg-zinc-950/30">
                        <TerminalSquare className="h-6 w-6 mb-2 opacity-30" />
                        <span className="text-[13px] font-medium text-center px-4">
                            {project.userRole <= 2 ? 'Ready for setup. Create your first flag.' : 'No flags yet'}
                        </span>
                    </div>
                ) : (
                    <div className="flex flex-col flex-1 justify-between gap-5">
                        <div className="grid grid-cols-2 gap-3">
                            <div className="flex flex-col gap-1.5 p-3 rounded-xl bg-zinc-900/30 border border-border/20">
                                <span className="text-[10px] text-muted-foreground font-semibold uppercase tracking-wider">Flags</span>
                                <div className="flex items-baseline gap-1.5">
                                    <span className="text-xl font-bold text-zinc-200">{project.activeFlags}</span>
                                    <span className="text-[12px] text-zinc-500 font-medium">/ {project.totalFlags}</span>
                                </div>
                            </div>
                            <div className="flex flex-col gap-1.5 p-3 rounded-xl bg-zinc-900/30 border border-border/20">
                                <span className="text-[10px] text-muted-foreground font-semibold uppercase tracking-wider">Requests (24h)</span>
                                {project.evaluations24H > 0 ? (
                                    <span className="text-xl font-bold text-sky-400">{formatEvals(project.evaluations24H)}</span>
                                ) : (
                                    <span className="text-xl font-bold text-zinc-600">0</span>
                                )}
                            </div>
                        </div>

                        <div className="flex flex-col gap-2.5 mt-auto">
                            {(project.runningExperiments > 0 || project.mabActiveFlagsCount > 0) && (
                                <div className="flex items-center gap-2 flex-wrap">
                                    {project.runningExperiments > 0 && (
                                        <div className="flex items-center gap-1.5 bg-emerald-500/10 text-emerald-400 px-2.5 py-1 rounded-md text-[11px] font-medium border border-emerald-500/15">
                                            <FlaskConical className="h-3 w-3" />
                                            <span>{project.runningExperiments} Active Test{project.runningExperiments > 1 ? 's' : ''}</span>
                                        </div>
                                    )}
                                    {project.mabActiveFlagsCount > 0 && (
                                        <div className="flex items-center gap-1.5 bg-amber-500/10 text-amber-400 px-2.5 py-1 rounded-md text-[11px] font-medium border border-amber-500/20">
                                            <Sparkles className="h-3 w-3" />
                                            <span>{project.mabActiveFlagsCount} Auto-Tuning</span>
                                        </div>
                                    )}
                                </div>
                            )}

                            <div className="flex flex-col gap-1.5 pt-1">
                                {project.topExperimentFlagKey && (
                                    <div className="flex items-center gap-2 text-[12px] text-muted-foreground">
                                        <FlaskConical className="h-3.5 w-3.5 text-emerald-500/70 shrink-0" />
                                        <span className="truncate">Top test: <span className="text-zinc-300 font-medium">{project.topExperimentFlagKey}</span></span>
                                    </div>
                                )}

                                {project.failingWebhooksCount > 0 && (
                                    <div className="flex items-center gap-2 text-[12px] text-rose-400 bg-rose-500/5 px-2 py-1 -ml-2 rounded-md">
                                        <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
                                        <span className="font-medium">{project.failingWebhooksCount} webhook{project.failingWebhooksCount > 1 ? 's' : ''} failing</span>
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </Card>
    );
}
