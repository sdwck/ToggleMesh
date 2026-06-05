using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Auth.Endpoints;

public class RegisterEndpoint : ToggleEndpoint<RegisterRequest>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RegisterEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email
        };

        var result = await _userManager.CreateAsync(user, req.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                AddError(error.Description);
            ThrowIfAnyErrors();
        }
        
        if (await _userManager.Users.CountAsync(ct) == 1)
            await _userManager.AddClaimAsync(user, new Claim("Role", "Owner"));

        await Send.OkAsync(cancellation: ct);
    }
}