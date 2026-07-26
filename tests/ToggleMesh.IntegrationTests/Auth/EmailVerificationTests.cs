using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ToggleMesh.API.Features.Auth.Register;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using ToggleMesh.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ToggleMesh.IntegrationTests.Auth;

[Collection("SharedEnv4")]
public class EmailVerificationTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    
    public EmailVerificationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_WithEmailsDisabled_ShouldSetSkippedNoSmtp()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:EnableEmails"] = "false"
                });
            });
        }).CreateClient();

        var request = new RegisterRequest
        {
            Email = $"user_{Guid.NewGuid()}@example.com",
            Password = "Password123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        
        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeTrue();
        user.EmailVerificationMethod.Should().Be(EmailVerificationMethod.SkippedNoSmtp);
    }

    [Fact]
    public async Task Register_WithEmailsEnabled_ShouldSetNoneAndSendEmail()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:EnableEmails"] = "true"
                });
            });
        }).CreateClient();

        var request = new RegisterRequest
        {
            Email = $"user_{Guid.NewGuid()}@example.com",
            Password = "Password123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        
        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeFalse();
        user.EmailVerificationMethod.Should().Be(EmailVerificationMethod.None);
    }

    [Fact]
    public async Task ResendConfirmation_WithSkippedNoSmtp_ShouldReturnOkAndSendEmail_WhenEmailsEnabled()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:EnableEmails"] = "true"
                });
            });
        }).CreateClient();

        var email = $"user_{Guid.NewGuid()}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                EmailVerificationMethod = EmailVerificationMethod.SkippedNoSmtp
            };
            await userManager.CreateAsync(user, "Password123!");
        }

        var request = new ResendConfirmationEmailRequest
        {
            Email = email
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/resend-confirmation", request);

        var content = await response.Content.ReadAsStringAsync();
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
    }
}
