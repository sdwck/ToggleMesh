import { Outlet, NavLink, useLocation, useParams, Link, useNavigate, Navigate } from 'react-router-dom';
import { LogOut, Flag, Network, Users, FileText, ArrowLeft, Settings, ToggleRight, Building2 } from 'lucide-react';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import { useMemo } from 'react';
import { jwtDecode } from "jwt-decode";
import { useQueryClient } from '@tanstack/react-query';
import { OrganizationSwitcher } from './OrganizationSwitcher';
import { ProjectSwitcher } from './ProjectSwitcher';
import { useRealTimeStream } from '@/hooks/useRealTimeStream';
import { useUserProfile, useProjectDetails, useOrganizations } from '@/api/queries';
import { useEffect } from 'react';
import { useOrganizationStore } from '@/stores/useOrganizationStore';
import { OrganizationRole } from '@/api/types';

export function AppLayout() {
    const token = localStorage.getItem('accessToken');
    if (!token) {
        return <Navigate to="/login" replace />;
    }

    useRealTimeStream();
    const { data: profile } = useUserProfile();

    const location = useLocation();
    const { projectId } = useParams();
    const navigate = useNavigate();

    const { data: project, isError: isProjectError } = useProjectDetails(projectId || '');
    const canManageProject = project?.userRole === 0 || project?.userRole === 1;

    const { activeOrganizationId } = useOrganizationStore();
    const { data: organizations } = useOrganizations();
    const activeOrg = organizations?.find(o => o.id === activeOrganizationId);
    const isOrgAdmin = activeOrg?.role === OrganizationRole.Admin;
    const queryClient = useQueryClient();

    useEffect(() => {
        if (projectId && isProjectError) {
            queryClient.invalidateQueries({ queryKey: ['projects'] });
            navigate('/projects', { replace: true });
        }
    }, [projectId, isProjectError, navigate, queryClient]);

    const userEmail = useMemo(() => {
        try {
            const token = localStorage.getItem('accessToken');
            if (!token) return 'User';
            const parsed: any = jwtDecode(token);
            return parsed.email || parsed['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || 'User';
        } catch {
            return 'User';
        }
    }, []);

    const displayName = profile?.username || userEmail;

    const userRole = useMemo(() => {
        try {
            const token = localStorage.getItem('accessToken');
            if (!token) return 'Member';
            const parsed: any = jwtDecode(token);
            return parsed.role || parsed['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || 'Member';
        } catch {
            return 'Member';
        }
    }, []);

    const handleLogout = () => {
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
        localStorage.removeItem('REACT_QUERY_OFFLINE_CACHE');
        window.location.href = '/login';
    };

    return (
        <div className="flex flex-col h-screen bg-background text-foreground overflow-hidden">
            <header
                className="h-14 border-b border-border/40 bg-zinc-950/80 backdrop-blur flex items-center justify-between px-6 z-10 shrink-0">
                <div className="flex items-center gap-2">
                    <Link to="/projects" className="flex items-center text-zinc-400 hover:text-white transition-colors mr-2">
                        <span className="font-semibold tracking-tight">ToggleMesh</span>
                    </Link>
                    <span className="text-zinc-700 text-sm font-light select-none">/</span>
                    <OrganizationSwitcher />
                    {projectId && (
                        <>
                            <span className="text-zinc-700 text-sm font-light select-none">/</span>
                            <ProjectSwitcher />
                        </>
                    )}
                </div>

                <div className="flex items-center gap-4">
                    <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                            <div className="flex items-center gap-3 cursor-pointer hover:opacity-80 transition-opacity select-none group">
                                <Avatar className="h-8 w-8 border border-white/5 ring-1 ring-black/50 transition-colors group-hover:border-white/10">
                                    <AvatarFallback className="bg-zinc-900 text-xs font-semibold text-zinc-200">
                                        {displayName.charAt(0).toUpperCase()}
                                    </AvatarFallback>
                                </Avatar>
                                <span className="text-xs font-medium text-zinc-400 group-hover:text-zinc-200 hidden sm:inline truncate max-w-[120px] transition-colors" title={displayName}>
                                    {displayName}
                                </span>
                            </div>
                        </DropdownMenuTrigger>

                        <DropdownMenuContent
                            align="end"
                            className="min-w-[12rem] max-w-[18rem] border border-white/10 bg-black/70 backdrop-blur-xl p-1.5 shadow-[0_16px_40px_rgba(0,0,0,0.8)] rounded-xl animate-in fade-in-50 slide-in-from-top-2 duration-200 z-[100]"
                        >
                            <DropdownMenuLabel className="font-normal p-3 w-full min-w-0 overflow-hidden">
                                <div className="flex flex-col space-y-3 w-full min-w-0">
                                    <div className="flex items-center gap-3 w-full min-w-0">
                                        <Avatar className="h-9 w-9 border border-white/10 shrink-0 shadow-sm">
                                            <AvatarFallback className="bg-zinc-800 text-xs font-medium text-zinc-200">
                                                {displayName.charAt(0).toUpperCase()}
                                            </AvatarFallback>
                                        </Avatar>

                                        <div className="flex flex-col min-w-0 flex-1 justify-center gap-0.5">
                                            <p className="text-sm font-semibold text-zinc-100 truncate block w-full" title={displayName}>
                                                {displayName}
                                            </p>
                                            {profile?.username && (
                                                <p className="text-[10px] text-zinc-500 truncate block w-full font-mono" title={profile.email}>
                                                    {profile.email}
                                                </p>
                                            )}
                                            <div className="flex mt-1">
                                                <span className="inline-flex items-center rounded bg-zinc-800/60 px-1.5 py-1 text-[9px] font-medium leading-none text-zinc-400 ring-1 ring-inset ring-white/5 capitalize">
                                                    {userRole}
                                                </span>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </DropdownMenuLabel>

                            <DropdownMenuSeparator className="bg-white/10 my-1" />
                            <DropdownMenuItem onClick={() => navigate('/settings/account')} className="cursor-pointer px-2.5 py-2 text-xs flex items-center gap-2 rounded-lg text-zinc-300 hover:text-white focus:bg-white/10 focus:text-white transition-all">
                                <Settings className="h-4 w-4 text-zinc-400" />
                                <span>Account Settings</span>
                            </DropdownMenuItem>
                            {isOrgAdmin && (
                                <DropdownMenuItem onClick={() => navigate('/settings/organization')} className="cursor-pointer px-2.5 py-2 text-xs flex items-center gap-2 rounded-lg text-zinc-300 hover:text-white focus:bg-white/10 focus:text-white transition-all">
                                    <Building2 className="h-4 w-4 text-zinc-400" />
                                    <span>Organization Settings</span>
                                </DropdownMenuItem>
                            )}
                            <DropdownMenuSeparator className="bg-white/10 my-1" />
                            <DropdownMenuItem onClick={handleLogout} className="text-red-400 focus:text-red-300 focus:bg-red-500/15 cursor-pointer px-2.5 py-2 text-xs flex items-center gap-2 rounded-lg transition-all">
                                <LogOut className="h-4 w-4" />
                                <span>Log out</span>
                            </DropdownMenuItem>
                        </DropdownMenuContent>
                    </DropdownMenu>
                </div>
            </header>

            <div className="flex flex-1 overflow-hidden">
                {projectId && (
                    <aside className="w-64 border-r border-border/40 bg-zinc-950/20 flex flex-col shrink-0">
                        <nav className="flex-1 py-6 px-3 space-y-1">
                            <Link
                                to="/projects"
                                className="flex items-center gap-2 px-3 py-2 text-xs font-semibold text-muted-foreground hover:text-foreground transition-colors mb-4"
                            >
                                <ArrowLeft className="h-3.5 w-3.5" />
                                Back to Projects
                            </Link>
                            <div
                                className="text-xs font-bold text-muted-foreground/60 px-3 uppercase tracking-wider mb-2">
                                Project Menu
                            </div>
                            <NavLink
                                to={`/projects/${projectId}/flags`}
                                className={({ isActive }) =>
                                    `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${isActive
                                        ? 'bg-primary/10 text-primary'
                                        : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                                    }`
                                }
                            >
                                <Flag className="h-4 w-4" />
                                Feature Flags
                            </NavLink>
                            <NavLink
                                to={`/projects/${projectId}/environments`}
                                className={({ isActive }) =>
                                    `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${isActive || location.pathname.includes(`/projects/${projectId}/environments/`)
                                        ? 'bg-primary/10 text-primary'
                                        : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                                    }`
                                }
                            >
                                <Network className="h-4 w-4" />
                                Environments
                            </NavLink>
                            {canManageProject && (
                                <NavLink
                                    to={`/projects/${projectId}/members`}
                                    className={({ isActive }) =>
                                        `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${isActive
                                            ? 'bg-primary/10 text-primary'
                                            : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                                        }`
                                    }
                                >
                                    <Users className="h-4 w-4" />
                                    Members
                                </NavLink>
                            )}
                            {project && project.userRole < 3 && (
                                <NavLink
                                    to={`/projects/${projectId}/audit`}
                                    className={({ isActive }) =>
                                        `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${isActive
                                            ? 'bg-primary/10 text-primary'
                                            : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                                        }`
                                    }
                                >
                                    <FileText className="h-4 w-4" />
                                    Audit Logs
                                </NavLink>
                            )}
                            <NavLink
                                to={`/projects/${projectId}/settings`}
                                className={({ isActive }) =>
                                    `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${isActive
                                        ? 'bg-primary/10 text-primary'
                                        : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                                    }`
                                }
                            >
                                <Settings className="h-4 w-4" />
                                Settings
                            </NavLink>
                            <NavLink
                                to={`/projects/${projectId}/playground`}
                                className={({ isActive }) =>
                                    `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${isActive
                                        ? 'bg-primary/10 text-primary'
                                        : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                                    }`
                                }
                            >
                                <ToggleRight className="h-4 w-4" />
                                Playground
                            </NavLink>
                        </nav>
                    </aside>
                )}

                <main className="flex-1 flex flex-col overflow-hidden">
                    <div className="flex-1 overflow-auto p-8">
                        <div className="max-w-6xl mx-auto h-full flex flex-col">
                            <Outlet />
                        </div>
                    </div>
                </main>
            </div>
        </div>
    );
}