namespace ToggleMesh.API.Features.Auth.TwoFactor;

public class GenerateRecoveryCodesResponse
{
    public IEnumerable<string> RecoveryCodes { get; set; } = [];
}