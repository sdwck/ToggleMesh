using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.TwoFactor;

public class EnableTwoFactorEndpoint : ToggleEndpoint<EnableTwoFactorRequest, EnableTwoFactorResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public EnableTwoFactorEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Post("/auth/2fa/enable");
        Version(1);
    }

    public override async Task HandleAsync(EnableTwoFactorRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(UserId.ToString());
        if (user == null)
            ThrowError("User not found", 404);

        var stripSpacesAndHyphens = req.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, stripSpacesAndHyphens);

        if (!is2faTokenValid)
            ThrowError("Verification code is invalid.", 400);

        await _userManager.SetTwoFactorEnabledAsync(user, true);

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        await Send.OkAsync(new EnableTwoFactorResponse
        {
            RecoveryCodes = recoveryCodes ?? []
        }, cancellation: ct);
    }
}
