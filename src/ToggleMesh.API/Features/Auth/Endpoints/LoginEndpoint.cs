using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Auth.Endpoints;

public class LoginEndpoint : ToggleEndpoint<LoginRequest, LoginResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public LoginEndpoint(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        
        if (user == null || !await _userManager.CheckPasswordAsync(user, req.Password))
            ThrowError("Invalid email or password");

        var jwtSettings = _configuration.GetSection("Jwt");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name, user.UserName!)
        };
        
        var userClaims = await _userManager.GetClaimsAsync(user);
        claims.AddRange(userClaims);
        
        var key = new SymmetricSecurityKey(
            Encoding.UTF8
                .GetBytes(jwtSettings["Key"] 
                          ?? throw new InvalidOperationException("JWT Key is not configured.")));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        var tokenHandler = new JwtSecurityTokenHandler();

        await Send.OkAsync(new LoginResponse
        {
            Token = tokenHandler.WriteToken(token)
        }, cancellation: ct);
    }
}