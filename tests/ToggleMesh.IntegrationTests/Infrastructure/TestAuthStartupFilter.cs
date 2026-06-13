using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using ToggleMesh.API.Persistence;

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
                    var claims = new[]
                    {
                        new Claim(JwtRegisteredClaimNames.Sub, TestAuthHandler.TestUserId),
                        new Claim(ClaimTypes.Email, TestAuthHandler.TestUserEmail),
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
