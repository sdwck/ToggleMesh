using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Auth.Login;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Infrastructure.Security.Authorization;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.Refresh;

public class RefreshEndpoint : ToggleEndpoint<RefreshRequest, LoginResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public RefreshEndpoint(
        UserManager<ApplicationUser> userManager, 
        IConfiguration configuration, 
        AppDbContext db, 
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _configuration = configuration;
        _db = db;
        _timeProvider = timeProvider;
    }

    public override void Configure()
    {
        Post("/auth/refresh");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(RefreshRequest req, CancellationToken ct)
    {
        var principal = await GetPrincipalFromExpiredTokenAsync(req.Token);
        if (principal == null)
        {
            ThrowError("Invalid access token or refresh token");
            return;
        }

        var userIdString = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userIdString == null || !Guid.TryParse(userIdString, out var userId))
        {
            ThrowError("Invalid token");
            return;
        }

        var rt = await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Token == req.RefreshToken, ct);

        var oldTokenGracePeriod = TimeSpan.FromSeconds(15);
        if (rt == null
            || rt.UserId != userId 
            || rt.Revoked != null 
            && rt.Revoked + oldTokenGracePeriod < _timeProvider.GetUtcNow())
        {
            ThrowError("Invalid access or refresh token.");
            return;
        }

        rt.Revoked ??= _timeProvider.GetUtcNow().UtcDateTime;

        var (newAccessToken, newRefreshToken) = await TokenGenerator.GenerateTokensAsync(rt.User, _userManager, _configuration, _timeProvider);
        
        if (!int.TryParse(
                _configuration["Auth:RefreshTokenLifetimeDays"], 
                out var tokenLifetime))
            tokenLifetime = 7;

        var newRt = new RefreshToken
        {
            Token = newRefreshToken,
            UserId = rt.UserId,
            Expires = _timeProvider.GetUtcNow().UtcDateTime.AddDays(tokenLifetime),
            Created = _timeProvider.GetUtcNow().UtcDateTime
        };

        _db.RefreshTokens.Add(newRt);
        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new LoginResponse
        {
            Token = newAccessToken,
            RefreshToken = newRefreshToken
        }, ct);
    }

    private async Task<ClaimsPrincipal?> GetPrincipalFromExpiredTokenAsync(string token)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var tokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = RsaKeyProvider.GetKey(_configuration),
            ValidateLifetime = false
        };

        var tokenHandler = new JsonWebTokenHandler();
        try
        {
            var result = await tokenHandler.ValidateTokenAsync(token, tokenValidationParameters);
            if (!result.IsValid)
                return null;

            if (result.SecurityToken is not JsonWebToken jwtSecurityToken || 
                !jwtSecurityToken.Alg.Equals(Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256, StringComparison.InvariantCultureIgnoreCase))
                return null;

            return new ClaimsPrincipal(result.ClaimsIdentity);
        }
        catch
        {
            return null;
        }
    }
}