using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Auth.ForgotPassword;
using ToggleMesh.API.Features.Auth.Login;
using ToggleMesh.API.Features.Auth.Register;
using ToggleMesh.API.Features.Auth.ResetPassword;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Auth;

[Collection("SharedEnv4")]
public class ForgotPasswordTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public ForgotPasswordTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task ConfirmUserEmailAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user != null)
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await userManager.ConfirmEmailAsync(user, token);
        }
    }

    [Fact]
    public async Task ForgotPassword_WithValidEmail_ShouldSucceed()
    {
        // Arrange
        var email = "forgot1@test.com";
        var password = "Password123!";
        var registerReq = new RegisterRequest { Email = email, Password = password };
        await _client.PostAsJsonAsync("/api/v1/auth/register", registerReq);
        await ConfirmUserEmailAsync(email);

        var forgotReq = new ForgotPasswordRequest { Email = email };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", forgotReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_WithInvalidEmail_ShouldStillReturnOk()
    {
        // Arrange
        var forgotReq = new ForgotPasswordRequest { Email = "nonexistent@test.com" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", forgotReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ShouldChangePasswordAndAllowLogin()
    {
        // Arrange
        var email = "forgot2@test.com";
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword123!";
        
        var registerReq = new RegisterRequest { Email = email, Password = oldPassword };
        await _client.PostAsJsonAsync("/api/v1/auth/register", registerReq);
        await ConfirmUserEmailAsync(email);

        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            var rawToken = await userManager.GeneratePasswordResetTokenAsync(user!);
            token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
        }

        var resetReq = new ResetPasswordRequest
        {
            Email = email,
            Token = token,
            NewPassword = newPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", resetReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginReq = new LoginRequest { Email = email, Password = newPassword };
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var loginOldReq = new LoginRequest { Email = email, Password = oldPassword };
        var loginOldResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginOldReq);
        loginOldResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
