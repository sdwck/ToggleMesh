// ReSharper disable MemberCanBePrivate.Global
namespace ToggleMesh.API.Features.Auth.Models;

public static class Permissions
{
    public const string ProjectsView = "Projects.View";
    public const string ProjectsCreate = "Projects.Create";
    public const string ProjectsEdit = "Projects.Edit";
    public const string ProjectsDelete = "Projects.Delete";
    public const string ProjectsManageMembers = "Projects.ManageMembers";
    
    public const string EnvironmentsView = "Environments.View";
    public const string EnvironmentsCreate = "Environments.Create";
    public const string EnvironmentsEdit = "Environments.Edit";
    public const string EnvironmentsDelete = "Environments.Delete";
    public const string EnvironmentsSync = "Environments.Sync";
    public const string EnvironmentsKeysRotate = "Environments.Keys.Rotate";
    
    public const string FlagsView = "Flags.View";
    public const string FlagsCreate = "Flags.Create";
    public const string FlagsToggle = "Flags.Toggle";
    public const string FlagsEdit = "Flags.Edit";
    public const string FlagsDelete = "Flags.Delete";
    
    public const string WebhooksCreate = "Webhooks.Create";
    
    public static readonly string[] OwnerPermissions = 
    [
        ProjectsView,
        ProjectsEdit,
        ProjectsCreate, 
        ProjectsDelete, 
        ProjectsManageMembers,
        EnvironmentsView,
        EnvironmentsEdit,
        EnvironmentsCreate, 
        EnvironmentsDelete, 
        EnvironmentsSync,
        EnvironmentsKeysRotate,
        FlagsView,
        FlagsCreate, 
        FlagsEdit, 
        FlagsToggle,
        FlagsDelete,
        WebhooksCreate
    ];
    
    public static readonly string[] AdminPermissions =
    [
        ProjectsView,
        ProjectsEdit,
        ProjectsManageMembers,
        EnvironmentsView,
        EnvironmentsCreate,
        EnvironmentsEdit,
        EnvironmentsDelete,
        EnvironmentsSync,
        EnvironmentsKeysRotate,
        FlagsView,
        FlagsCreate,
        FlagsEdit,
        FlagsToggle,
        FlagsDelete,
        WebhooksCreate
    ];
    
    public static readonly string[] EditorPermissions = 
    [
        ProjectsView,
        EnvironmentsView,
        FlagsView,
        FlagsToggle,
        FlagsEdit
    ];
    
    public static readonly string[] ViewerPermissions = 
    [
        ProjectsView,
        EnvironmentsView,
        FlagsView
    ];
}