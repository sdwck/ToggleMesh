import { Outlet, NavLink, useLocation, useParams, Link } from 'react-router-dom';
import { LayoutDashboard, LogOut, Flag, Network, Users, FileText, ArrowLeft } from 'lucide-react';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { useMemo } from 'react';

export function AppLayout() {
  const location = useLocation();
  const { projectId } = useParams();

  const userEmail = useMemo(() => {
    try {
      const token = localStorage.getItem('accessToken');
      if (!token) return 'User';
      const base64Url = token.split('.')[1];
      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      const jsonPayload = decodeURIComponent(atob(base64).split('').map(function(c) {
          return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
      }).join(''));
      const parsed = JSON.parse(jsonPayload);
      return parsed.email || parsed['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || 'User';
    } catch {
      return 'User';
    }
  }, []);

  const handleLogout = () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    window.location.href = '/login';
  };

  return (
    <div className="flex flex-col h-screen bg-background text-foreground overflow-hidden">
      <header className="h-14 border-b border-border/40 bg-zinc-950/80 backdrop-blur flex items-center justify-between px-6 z-10 shrink-0">
        <div className="flex items-center gap-3">
          <Link to="/" className="flex items-center">
            <span className="font-semibold tracking-tight text-primary">ToggleMesh</span>
          </Link>
          <Badge className="bg-primary/10 text-primary hover:bg-primary/20" variant="secondary">
            BETA
          </Badge>
        </div>

        <div className="flex items-center gap-4">
          <div className="flex items-center gap-3">
            <Avatar className="h-8 w-8 border border-border/40">
              <AvatarFallback className="bg-muted text-xs">{userEmail.charAt(0).toUpperCase()}</AvatarFallback>
            </Avatar>
            <span className="text-xs font-medium text-muted-foreground hidden sm:inline" title={userEmail}>
              {userEmail}
            </span>
          </div>
          <button
            onClick={handleLogout}
            className="text-muted-foreground hover:text-foreground transition-colors p-1"
            title="Logout"
          >
            <LogOut className="h-4 w-4" />
          </button>
        </div>
      </header>

      <div className="flex flex-1 overflow-hidden">
        <aside className="w-64 border-r border-border/40 bg-zinc-950/20 flex flex-col shrink-0">
          <nav className="flex-1 py-6 px-3 space-y-1">
            {projectId ? (
              <>
                <Link
                  to="/projects"
                  className="flex items-center gap-2 px-3 py-2 text-xs font-semibold text-muted-foreground hover:text-foreground transition-colors mb-4"
                >
                  <ArrowLeft className="h-3.5 w-3.5" />
                  Back to Projects
                </Link>
                <div className="text-xs font-bold text-muted-foreground/60 px-3 uppercase tracking-wider mb-2">
                  Project Menu
                </div>
                <NavLink
                  to={`/projects/${projectId}/flags`}
                  className={({ isActive }) =>
                    `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                      isActive
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
                    `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                      isActive || location.pathname.includes(`/projects/${projectId}/environments/`)
                        ? 'bg-primary/10 text-primary'
                        : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                    }`
                  }
                >
                  <Network className="h-4 w-4" />
                  Environments
                </NavLink>
                <NavLink
                  to={`/projects/${projectId}/members`}
                  className={({ isActive }) =>
                    `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                      isActive
                        ? 'bg-primary/10 text-primary'
                        : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                    }`
                  }
                >
                  <Users className="h-4 w-4" />
                  Members
                </NavLink>
                <NavLink
                  to={`/projects/${projectId}/audit`}
                  className={({ isActive }) =>
                    `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                      isActive
                        ? 'bg-primary/10 text-primary'
                        : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                    }`
                  }
                >
                  <FileText className="h-4 w-4" />
                  Audit Logs
                </NavLink>
              </>
            ) : (
              <>
                <div className="text-xs font-bold text-muted-foreground/60 px-3 uppercase tracking-wider mb-2">
                  Navigation
                </div>
                <NavLink
                  to="/projects"
                  className={({ isActive }) =>
                    `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                      isActive || location.pathname === '/'
                        ? 'bg-primary/10 text-primary'
                        : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                    }`
                  }
                >
                  <LayoutDashboard className="h-4 w-4" />
                  Projects
                </NavLink>
              </>
            )}
          </nav>
        </aside>
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
