namespace ToggleMesh.API.Features.Auth.TwoFactor;

public class DisableTwoFactorRequest
{
    public string Code { get; set; } = string.Empty;
}