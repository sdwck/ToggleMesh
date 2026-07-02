namespace ToggleMesh.API.Features.Projects.Domain;

public static class RoleHierarchy
{
    public static bool CanManageMember(ProjectRole actorRole, ProjectRole targetCurrentRole, ProjectRole? targetNewRole = null)
    {
        if (actorRole == ProjectRole.Owner)
            return true;

        if (actorRole == ProjectRole.Admin)
        {
            if (targetCurrentRole == ProjectRole.Owner)
                return false;
            
            if (targetNewRole.HasValue && targetNewRole.Value is ProjectRole.Owner or ProjectRole.Admin)
                return false;
                
            return true;
        }

        return false;
    }
}
