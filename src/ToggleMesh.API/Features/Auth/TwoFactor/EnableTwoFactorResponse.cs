namespace ToggleMesh.API.Features.Auth.TwoFactor;

public class EnableTwoFactorResponse
{
    public IEnumerable<string> RecoveryCodes { get; set; } = [];
}
