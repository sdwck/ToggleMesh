using System.Security.Claims;
using System.Text.Encodings.Web;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Infrastructure.Security;

public class PatAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AppDbContext _db;

    public PatAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext db) : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("x-pat-token", out var patHeader))
            return AuthenticateResult.NoResult();

        var patToken = patHeader.ToString();
        if (string.IsNullOrWhiteSpace(patToken))
            return AuthenticateResult.NoResult();

        var tokenHash = ApiKeyHasher.Hash(patToken);
        var pat = await _db.PersonalAccessTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => 
                x.TokenHash == tokenHash && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow));

        if (pat is null)
            return AuthenticateResult.Fail("Invalid or expired Personal Access Token.");

        await _db.PersonalAccessTokens
            .Where(x => x.Id == pat.Id)
            .ExecuteUpdateAsync(s => 
                s.SetProperty(t => 
                    t.LastUsedAt, DateTime.UtcNow));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, pat.UserId.ToString()),
            new Claim("amr", "pat")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}