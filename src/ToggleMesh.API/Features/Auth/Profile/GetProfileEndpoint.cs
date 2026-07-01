using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Auth.Endpoints.Profile;

public record UserProfileDto(Guid Id, string Email, string Username);

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
