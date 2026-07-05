
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Infrastructure.Security.Authorization;

public static class TokenGenerator
{
    public static async Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(
        ApplicationUser user, 
        UserManager<ApplicationUser> userManager, 
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("email", user.Email ?? ""),
            new("username", user.UserName ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        };
        
        var userClaims = await userManager.GetClaimsAsync(user);
        claims.AddRange(userClaims);

        var key = RsaKeyProvider.GetKey(configuration);
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            Expires = now.AddMinutes(15),
            SigningCredentials = creds
        };

        var tokenHandler = new JsonWebTokenHandler();
        var accessToken = tokenHandler.CreateToken(tokenDescriptor);

        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        var refreshToken = Convert.ToBase64String(randomNumber);

        return (accessToken, refreshToken);
    }
}