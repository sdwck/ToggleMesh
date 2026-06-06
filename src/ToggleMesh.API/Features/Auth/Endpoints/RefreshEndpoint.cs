using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;

namespace ToggleMesh.API.Features.Auth.Endpoints;

public class RefreshEndpoint : ToggleEndpoint<RefreshRequest, LoginResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;

    public RefreshEndpoint(UserManager<ApplicationUser> userManager, IConfiguration configuration, AppDbContext db)
    {
        _userManager = userManager;
        _configuration = configuration;
        _db = db;
    }

    public override void Configure()
    {
        Post("/auth/refresh");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RefreshRequest req, CancellationToken ct)
    {
        var principal = GetPrincipalFromExpiredToken(req.Token);
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

        if (rt == null || !rt.IsActive || rt.UserId != userId)
        {
            ThrowError("Invalid access token or refresh token");
            return;
        }

        rt.Revoked = DateTime.UtcNow;

        var (newAccessToken, newRefreshToken) = await TokenGenerator.GenerateTokensAsync(rt.User, _userManager, _configuration);

        var newRt = new RefreshToken
        {
            Token = newRefreshToken,
            UserId = rt.UserId,
            Expires = DateTime.UtcNow.AddDays(7),
            Created = DateTime.UtcNow
        };

        _db.RefreshTokens.Add(newRt);
        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new LoginResponse
        {
            Token = newAccessToken,
            RefreshToken = newRefreshToken
        }, ct);
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var tokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = Infrastructure.Security.RsaKeyProvider.GetKey(),
            ValidateLifetime = false
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken || 
                !jwtSecurityToken.Header.Alg.Equals(Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256, StringComparison.InvariantCultureIgnoreCase))
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}