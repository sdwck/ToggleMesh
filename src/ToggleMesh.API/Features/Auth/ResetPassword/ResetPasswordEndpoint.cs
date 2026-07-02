using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.ResetPassword;

public class ResetPasswordEndpoint : ToggleEndpoint<ResetPasswordRequest>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ResetPasswordEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Post("/auth/reset-password");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(ResetPasswordRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        
        if (user == null)
            ThrowError("Invalid email or token.");

        var decodedToken = string.Empty;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.Token));
        }
        catch (FormatException)
        {
            ThrowError("Invalid token format.");
        }

        var result = await _userManager.ResetPasswordAsync(user, decodedToken, req.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                AddError(error.Description);
            ThrowIfAnyErrors();
        }

        await Send.OkAsync(cancellation: ct);
    }
}
