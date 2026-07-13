using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.ChangePassword;

public class ChangePasswordEndpoint : ToggleEndpoint<ChangePasswordRequest>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ChangePasswordEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Post("/auth/change-password");
        Version(1);
    }

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(UserId.ToString());
        if (user == null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var result = await _userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                AddError(error.Description);
            ThrowIfAnyErrors();
        }

        await Send.OkAsync(cancellation: ct);
    }
}
