using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using FastEndpoints;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Infrastructure.Security.Authorization;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.Login;

public class VerifyTwoFactorLoginEndpoint : ToggleEndpoint<VerifyTwoFactorLoginRequest, LoginResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public VerifyTwoFactorLoginEndpoint(UserManager<ApplicationUser> userManager, IConfiguration configuration, AppDbContext db, TimeProvider timeProvider)
    {
        _userManager = userManager;
        _configuration = configuration;
        _db = db;
        _timeProvider = timeProvider;
    }

    public override void Configure()
    {
        Post("/auth/login/2fa");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(VerifyTwoFactorLoginRequest req, CancellationToken ct)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = RsaKeyProvider.GetKey(_configuration);

        var tokenHandler = new JsonWebTokenHandler
        {
            MapInboundClaims = false
        };

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true
        };

        try
        {
            var result = await tokenHandler.ValidateTokenAsync(req.TwoFactorToken, validationParameters);

            if (!result.IsValid)
                ThrowError("Invalid or expired two-factor token", 401);

            var principal = new ClaimsPrincipal(result.ClaimsIdentity);

            if (!principal.HasClaim(c => c.Type == "amr" && c.Value == "mfa_pending"))
                ThrowError("Invalid two-factor token", 401);

            var userIdString = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!Guid.TryParse(userIdString, out var userId))
                ThrowError("Invalid user identity", 401);

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is not { TwoFactorEnabled: true })
                ThrowError("Invalid user or 2FA not enabled", 401);

            var codeNoSpaces = req.Code.Replace(" ", string.Empty);
            var is2FaTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
                user, _userManager.Options.Tokens.AuthenticatorTokenProvider, codeNoSpaces.Replace("-", string.Empty));

            if (!is2FaTokenValid)
            {
                var isRecoveryCodeValid = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, codeNoSpaces);
                if (!isRecoveryCodeValid.Succeeded)
                    ThrowError("Invalid two-factor code", 401);
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
        catch (Exception ex) when (ex is not ValidationFailureException)
        {
            ThrowError("Invalid or expired two-factor token", 401);
        }
    }
}
