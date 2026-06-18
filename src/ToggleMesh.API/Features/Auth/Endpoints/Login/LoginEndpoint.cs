using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Auth.Endpoints.Login;

public class LoginEndpoint : ToggleEndpoint<LoginRequest, LoginResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;

    public LoginEndpoint(UserManager<ApplicationUser> userManager, IConfiguration configuration, AppDbContext db)
    {
        _userManager = userManager;
        _configuration = configuration;
        _db = db;
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
            ThrowError("Invalid email or password");

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

        await Send.OkAsync(new LoginResponse
        {
            Token = accessToken,
            RefreshToken = refreshToken
        }, cancellation: ct);
    }
}