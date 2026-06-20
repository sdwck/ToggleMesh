using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Features.Auth.Endpoints.Login;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Auth.Sso;

[AllowAnonymous]
public class SsoCallbackEndpoint : ToggleEndpoint<EmptyRequest, LoginResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;

    public SsoCallbackEndpoint(UserManager<ApplicationUser> userManager, IConfiguration configuration, AppDbContext db)
    {
        _userManager = userManager;
        _configuration = configuration;
        _db = db;
    }

    public override void Configure()
    {
        Get("/auth/sso/callback-handler");
        Version(1);
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var result = await HttpContext.AuthenticateAsync("TempCookie");

        if (!result.Succeeded || result.Principal == null)
        {
            ThrowError("Unauthorized");
            return;
        }

        var email = result.Principal.FindFirstValue(ClaimTypes.Email) 
            ?? result.Principal.FindFirstValue("email");

        if (string.IsNullOrEmpty(email))
        {
            ThrowError("Unauthorized");
            return;
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                ThrowError("Could not provision SSO user");
            }
        }

        var (accessToken, refreshToken) = await TokenGenerator.GenerateTokensAsync(user, _userManager, _configuration);

        var rt = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            Expires = DateTime.UtcNow.AddDays(7),
            Created = DateTime.UtcNow
        };
        
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync(ct);

        await HttpContext.SignOutAsync("TempCookie");

        await Send.OkAsync(new LoginResponse
        {
            Token = accessToken,
            RefreshToken = refreshToken
        }, cancellation: ct);
    }
}
