namespace ToggleMesh.API.Features.System.GetConfig;

public record PasswordPolicyDto(int MinimumLength, bool RequireDigit, bool RequireLowercase, bool RequireUppercase, bool RequireNonAlphanumeric);

public record SystemConfigResponse(bool AllowOpenRegistration, bool AllowUserOrganizationCreation, PasswordPolicyDto PasswordPolicy);