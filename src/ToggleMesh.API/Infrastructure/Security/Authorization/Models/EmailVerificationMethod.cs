namespace ToggleMesh.API.Infrastructure.Security.Authorization.Models;

public enum EmailVerificationMethod
{
    None = 0,
    Email = 1,
    Admin = 2,
    SkippedNoSmtp = 3,
    Sso = 4
}
