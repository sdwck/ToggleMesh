using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Auth.Endpoints.Profile;

public record UpdateProfileRequest(string Username);

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
        {
            AddError("Username cannot be empty.");
            await Send.ErrorsAsync(400, cancellation: ct);
            return;
        }

        var user = await _userManager.FindByIdAsync(UserId.ToString());
        if (user == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var existing = await _userManager.FindByNameAsync(req.Username.Trim());
        if (existing != null && existing.Id != user.Id)
        {
            AddError("Username is already taken.");
            await Send.ErrorsAsync(400, cancellation: ct);
            return;
        }

        user.UserName = req.Username.Trim();
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
            {
                AddError(err.Description);
            }
            await Send.ErrorsAsync(400, cancellation: ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
