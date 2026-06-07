import { Outlet, NavLink, useLocation } from 'react-router-dom';
import { LayoutDashboard, LogOut } from 'lucide-react';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Separator } from '@/components/ui/separator';
import { Badge } from '@/components/ui/badge';

export function AppLayout() {
  const location = useLocation();

  const handleLogout = () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    window.location.href = '/login';
  };

  return (
    <div className="flex h-screen bg-background text-foreground overflow-hidden">
      <aside className="w-64 border-r border-border/40 bg-zinc-950/50 flex flex-col">
        <div className="h-14 flex items-center px-6 border-b border-border/40">
          <span className="font-semibold tracking-tight text-primary">ToggleMesh</span>
          <Badge className="ml-2 bg-primary/10 text-primary hover:bg-primary/20" variant="secondary">
            BETA
          </Badge>
        </div>

        <nav className="flex-1 py-4 px-3 space-y-1">
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
        </nav>

        <div className="p-4 mt-auto">
          <Separator className="mb-4 bg-border/40" />
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <Avatar className="h-8 w-8 border border-border/40">
                <AvatarFallback className="bg-muted text-xs">U</AvatarFallback>
              </Avatar>
              <div className="flex flex-col">
                <span className="text-xs font-medium">User</span>
              </div>
            </div>
            <button
              onClick={handleLogout}
              className="text-muted-foreground hover:text-foreground transition-colors p-1"
              title="Logout"
            >
              <LogOut className="h-4 w-4" />
            </button>
          </div>
        </div>
      </aside>

      <main className="flex-1 flex flex-col h-screen overflow-hidden">
        <header className="h-14 flex items-center px-8 border-b border-border/40 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
        </header>

        <div className="flex-1 overflow-auto p-8">
          <div className="max-w-6xl mx-auto">
            <Outlet />
          </div>
        </div>
      </main>
    </div>
  );
}