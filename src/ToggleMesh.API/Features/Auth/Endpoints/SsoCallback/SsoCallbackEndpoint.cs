using System.Security.Claims;
using System.Text.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using StackExchange.Redis;
using ToggleMesh.API.Features.Auth.Endpoints.SsoTicketExchange;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Auth.Endpoints.SsoCallback;

public class SsoCallbackEndpoint : EndpointWithoutRequest
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;
    private readonly IDatabase _redis;

    public SsoCallbackEndpoint(
        UserManager<ApplicationUser> userManager, 
        IConfiguration configuration, 
        AppDbContext db,
        IConnectionMultiplexer redisConnection)
    {
        _userManager = userManager;
        _configuration = configuration;
        _db = db;
        _redis = redisConnection.GetDatabase();
    }

    public override void Configure()
    {
        Get("/auth/sso/callback-handler");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
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
                ThrowError("Could not provision SSO user");
        }

        var (accessToken, refreshToken) = await TokenGenerator.GenerateTokensAsync(user, _userManager, _configuration);
        
        if (!int.TryParse(
                _configuration["Auth:RefreshTokenLifetimeDays"], 
                out var tokenLifetime))
            tokenLifetime = 7;

        var rt = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            Expires = DateTime.UtcNow.AddDays(tokenLifetime),
            Created = DateTime.UtcNow
        };
        
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync(ct);

        await HttpContext.SignOutAsync("TempCookie");

        var ticket = Guid.CreateVersion7().ToString();
        var key = $"sso:ticket:{ticket}";
        var data = JsonSerializer.Serialize(new SsoTicketData
        {
            AccessToken = accessToken, 
            RefreshToken = refreshToken
        });
        
        await _redis.StringSetAsync(key, data, TimeSpan.FromSeconds(30));

        var frontendUrl = _configuration["Auth:FrontendUrl"] 
                          ?? "http://localhost:5173";
        var redirectUrl = $"{frontendUrl.TrimEnd('/')}/login?ticket={ticket}";

        await Send.RedirectAsync(redirectUrl, allowRemoteRedirects: true);
    }
}
