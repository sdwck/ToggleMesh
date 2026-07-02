using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.UpdateProfile;

public class UpdateProfileEndpoint : ToggleEndpoint<UpdateProfileRequest>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateProfileEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Put("/user/profile");
        Version(1);
    }

    public override async Task HandleAsync(UpdateProfileRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Username))
            ThrowError("Username cannot be empty.", 400);

        var user = await _userManager.FindByIdAsync(UserId.ToString());
        if (user == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var existing = await _userManager.FindByNameAsync(req.Username.Trim());
        if (existing != null && existing.Id != user.Id)
            ThrowError("Username is already taken.", 400);

        user.UserName = req.Username.Trim();
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                AddError(err.Description);
            ThrowIfAnyErrors();
        }

        await Send.NoContentAsync(ct);
    }
}
