using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Auth.Endpoints.ConfirmEmail;

public class ConfirmEmailEndpoint : ToggleEndpoint<ConfirmEmailRequest>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ConfirmEmailEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Post("/auth/confirm-email");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ConfirmEmailRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(req.UserId.ToString());
        if (user == null)
            ThrowError("Invalid request");

        var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.Token));
        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "ConcurrencyFailure"))
            {
                var reloadedUser = await _userManager.FindByIdAsync(req.UserId.ToString());
                if (reloadedUser is { EmailConfirmed: true })
                {
                    await Send.OkAsync(cancellation: ct);
                    return;
                }
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            ThrowError($"Invalid or expired confirmation token. Errors: {errors}");
        }

        await Send.OkAsync(cancellation: ct);
    }
}
