using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ToggleMesh.IntegrationTests.Infrastructure;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    public const string TestUserId = "00000000-0000-0000-0000-000000000001";
    public const string TestUserEmail = "test@example.com";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = TestUserId;
        var email = TestUserEmail;

        if (Request.Headers.TryGetValue("x-test-user-id", out var customUserId))
        {
            userId = customUserId.ToString();
            email = $"user-{userId}@example.com";
        }

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim("role", "Owner")
        };
        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

        var result = AuthenticateResult.Success(ticket);
        return Task.FromResult(result);
    }
}

public class TestTempCookieHandler : SignOutAuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestTempCookieHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue("x-test-temp-cookie-email", out var email))
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, email.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())
            };
            var identity = new ClaimsIdentity(claims, "TempCookie");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TempCookie");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        return Task.FromResult(AuthenticateResult.Fail("No x-test-temp-cookie-email header"));
    }

    protected override Task HandleSignOutAsync(AuthenticationProperties? properties)
    {
        return Task.CompletedTask;
    }
}
