using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Features.Auth.Endpoints.Login;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Auth.Endpoints.Register;

public class RegisterEndpoint : ToggleEndpoint<RegisterRequest, LoginResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;

    public RegisterEndpoint(UserManager<ApplicationUser> userManager, IConfiguration configuration, AppDbContext db)
    {
        _userManager = userManager;
        _configuration = configuration;
        _db = db;
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