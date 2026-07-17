namespace ToggleMesh.API.Features.Auth.TwoFactor;

public class EnableTwoFactorRequest
{
    public string Code { get; set; } = string.Empty;
}
