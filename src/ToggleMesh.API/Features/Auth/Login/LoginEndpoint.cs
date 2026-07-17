using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security.Authorization;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.Login;

public class LoginEndpoint : ToggleEndpoint<LoginRequest, LoginResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public LoginEndpoint(UserManager<ApplicationUser> userManager, IConfiguration configuration, AppDbContext db, TimeProvider timeProvider)
    {
        _userManager = userManager;
        _configuration = configuration;
        _db = db;
        _timeProvider = timeProvider;
    }

    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        
        if (user == null || !await _userManager.CheckPasswordAsync(user, req.Password))
            ThrowError("Invalid email or password", 401);

        if (!user.EmailConfirmed)
            ThrowError("Please confirm your email address before logging in.", 401);

        if (user.TwoFactorEnabled)
        {
            var twoFactorToken = TokenGenerator.GenerateTwoFactorToken(user, _configuration, _timeProvider);
            await Send.OkAsync(new LoginResponse
            {
                RequiresTwoFactor = true,
                TwoFactorToken = twoFactorToken
            }, cancellation: ct);
            return;
        }

        var (accessToken, refreshToken) = await TokenGenerator.GenerateTokensAsync(user, _userManager, _configuration, _timeProvider);

        if (!int.TryParse(
                _configuration["Auth:RefreshTokenLifetimeDays"], 
                out var tokenLifetime))
            tokenLifetime = 7;
        
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var rt = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            Expires = now.AddDays(tokenLifetime),
            Created = now
        };
        
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new LoginResponse
        {
            Token = accessToken,
            RefreshToken = refreshToken
        }, cancellation: ct);
    }
}
