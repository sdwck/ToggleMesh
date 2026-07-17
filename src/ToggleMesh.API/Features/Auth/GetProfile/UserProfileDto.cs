namespace ToggleMesh.API.Features.Auth.GetProfile;

public record UserProfileDto(Guid Id, string Email, string Username, bool TwoFactorEnabled, int RecoveryCodesLeft);
