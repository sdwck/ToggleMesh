namespace ToggleMesh.API.Features.Auth.Login;

public class VerifyTwoFactorLoginRequest
{
    public string TwoFactorToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
