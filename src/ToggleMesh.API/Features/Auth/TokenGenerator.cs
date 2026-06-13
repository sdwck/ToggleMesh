using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure.Security;

namespace ToggleMesh.API.Features.Auth;

public static class TokenGenerator
{
    public static async Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(
        ApplicationUser user, 
        UserManager<ApplicationUser> userManager, 
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("email", user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        
        var userClaims = await userManager.GetClaimsAsync(user);
        claims.AddRange(userClaims);

        var key = RsaKeyProvider.GetKey(configuration);
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(15),
            signingCredentials: creds
        )
        {
            Payload =
            {
                ["iat"] = new DateTimeOffset(now).ToUnixTimeSeconds()
            }
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var accessToken = tokenHandler.WriteToken(token);

        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        var refreshToken = Convert.ToBase64String(randomNumber);

        return (accessToken, refreshToken);
    }
}