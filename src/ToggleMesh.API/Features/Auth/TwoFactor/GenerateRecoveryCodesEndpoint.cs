using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.TwoFactor;

public class GenerateRecoveryCodesEndpoint : ToggleEndpoint<GenerateRecoveryCodesRequest, GenerateRecoveryCodesResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public GenerateRecoveryCodesEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Post("/auth/2fa/recovery-codes");
        Version(1);
    }

    public override async Task HandleAsync(GenerateRecoveryCodesRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(UserId.ToString());
        if (user == null)
            ThrowError("User not found", 404);

        if (!user.TwoFactorEnabled)
            ThrowError("Two-factor authentication is not enabled.", 400);

        var codeNoSpaces = req.Code.Replace(" ", string.Empty);
        var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, codeNoSpaces.Replace("-", string.Empty));

        if (!is2faTokenValid)
        {
            var isRecoveryCodeValid = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, codeNoSpaces);
            if (!isRecoveryCodeValid.Succeeded)
                ThrowError("Verification code is invalid.", 400);
        }

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        await Send.OkAsync(new GenerateRecoveryCodesResponse
        {
            RecoveryCodes = recoveryCodes ?? []
        }, cancellation: ct);
    }
}
