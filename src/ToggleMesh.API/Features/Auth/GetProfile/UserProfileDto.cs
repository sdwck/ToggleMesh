using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.GetProfile;

public record UserProfileDto(Guid Id, string Email, string Username, bool TwoFactorEnabled, int RecoveryCodesLeft, EmailVerificationMethod EmailVerificationMethod, bool SkipLandingPage);
