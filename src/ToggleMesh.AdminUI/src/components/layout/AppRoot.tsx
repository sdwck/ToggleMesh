import { Outlet, useLocation } from 'react-router-dom';
import { useEffect } from 'react';

export function AppRoot() {
    const location = useLocation();

    useEffect(() => {
        const path = location.pathname;
        let title = "ToggleMesh";
        
        if (path.includes('/login')) title = "Login | ToggleMesh";
        else if (path.includes('/register')) title = "Register | ToggleMesh";
        else if (path.includes('/forgot-password')) title = "Forgot Password | ToggleMesh";
        else if (path.includes('/reset-password')) title = "Reset Password | ToggleMesh";
        else if (path.includes('/confirm-email')) title = "Confirm Email | ToggleMesh";
        else if (path.includes('/invites')) title = "Accept Invite | ToggleMesh";
        else if (path.includes('/support')) title = "Support | ToggleMesh";
        else if (path.includes('/settings/account')) title = "Account Settings | ToggleMesh";
        else if (path.includes('/settings/organization')) title = "Organization Settings | ToggleMesh";
        else if (path.includes('/flags')) title = "Flags | ToggleMesh";
        else if (path.includes('/environments')) title = "Environments | ToggleMesh";
        else if (path.includes('/members')) title = "Members | ToggleMesh";
        else if (path.includes('/audit')) title = "Audit Log | ToggleMesh";
        else if (path.includes('/experiments')) title = "Experiments | ToggleMesh";
        else if (path.includes('/terminal')) title = "Terminal | ToggleMesh";
        else if (path.includes('/settings')) title = "Project Settings | ToggleMesh";
        else if (path.includes('/playground')) title = "API Playground | ToggleMesh";
        else if (path.match(/^\/projects\/[a-zA-Z0-9-]+$/)) title = "Dashboard | ToggleMesh";
        else if (path === '/projects' || path === '/projects/') title = "Projects | ToggleMesh";

        document.title = title;
    }, [location.pathname]);

    return <Outlet />;
}
