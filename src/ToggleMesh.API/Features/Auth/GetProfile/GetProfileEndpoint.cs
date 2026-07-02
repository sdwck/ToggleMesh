using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.GetProfile;

public class GetProfileEndpoint : ToggleEndpointWithoutRequest<UserProfileDto>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public GetProfileEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Get("/user/profile");
        Version(1);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(UserId.ToString());
        if (user == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new UserProfileDto(user.Id, user.Email ?? "", user.UserName ?? ""), ct);
    }
}
