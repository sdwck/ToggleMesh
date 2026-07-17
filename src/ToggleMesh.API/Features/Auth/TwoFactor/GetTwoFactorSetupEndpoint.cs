using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using System.Text.Encodings.Web;

namespace ToggleMesh.API.Features.Auth.TwoFactor;

public class GetTwoFactorSetupEndpoint : ToggleEndpointWithoutRequest<TwoFactorSetupResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UrlEncoder _urlEncoder;

    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    public GetTwoFactorSetupEndpoint(UserManager<ApplicationUser> userManager, UrlEncoder urlEncoder)
    {
        _userManager = userManager;
        _urlEncoder = urlEncoder;
    }

    public override void Configure()
    {
        Get("/auth/2fa/setup");
        Version(1);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(UserId.ToString());
        if (user == null)
            ThrowError("User not found", 404);

        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = await _userManager.GetEmailAsync(user);
        var authenticatorUri = string.Format(
            AuthenticatorUriFormat,
            _urlEncoder.Encode("ToggleMesh"),
            _urlEncoder.Encode(email!),
            unformattedKey);

        await Send.OkAsync(new TwoFactorSetupResponse
        {
            SharedKey = unformattedKey!,
            AuthenticatorUri = authenticatorUri
        }, cancellation: ct);
    }
}
