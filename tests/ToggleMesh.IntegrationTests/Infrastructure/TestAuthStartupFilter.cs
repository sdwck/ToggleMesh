using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ToggleMesh.IntegrationTests.Infrastructure;

public class TestAuthStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            builder.Use(async (context, nextMiddleware) =>
            {
                if (!context.Request.Headers.ContainsKey("x-pat-token") &&
                    !context.Request.Headers.ContainsKey("x-api-key"))
                {
                    var userId = TestAuthHandler.TestUserId;
                    var email = TestAuthHandler.TestUserEmail;

                    if (context.Request.Headers.TryGetValue("x-test-user-id", out var customUserId))
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
                    var identity = new ClaimsIdentity(claims, TestAuthHandler.AuthenticationScheme);
                    context.User = new ClaimsPrincipal(identity);
                }
                await nextMiddleware();
            });
            next(builder);
        };
    }
}
