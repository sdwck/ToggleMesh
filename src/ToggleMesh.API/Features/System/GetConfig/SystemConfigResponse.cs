namespace ToggleMesh.API.Features.System.GetConfig;

public record SystemConfigResponse(
    bool AllowOpenRegistration, 
    bool AllowUserOrganizationCreation, 
    PasswordPolicyDto PasswordPolicy,
    bool AnalyticsEnabled = true,
    bool EnableEmails = false
);