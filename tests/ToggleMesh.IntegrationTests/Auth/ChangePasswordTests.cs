using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Auth.ChangePassword;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Auth;

[Collection("SharedEnv1")]
public class ChangePasswordTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public ChangePasswordTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChangePassword_WithValidCredentials_ShouldSucceed()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(TestAuthHandler.TestUserEmail);
        await userManager.AddPasswordAsync(user!, "Password123!");

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "Password123!",
            NewPassword = "NewPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        using var assertScope = _factory.Services.CreateScope();
        var assertUserManager = assertScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updatedUser = await assertUserManager.FindByEmailAsync(TestAuthHandler.TestUserEmail);

        var isNewPasswordValid = await assertUserManager.CheckPasswordAsync(updatedUser!, request.NewPassword);
        isNewPasswordValid.Should().BeTrue("The password should have been updated in the database");
    }

    [Fact]
    public async Task ChangePassword_WithInvalidCurrentPassword_ShouldReturn400()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(TestAuthHandler.TestUserEmail);
        await userManager.AddPasswordAsync(user!, "Password123!");

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "WrongPassword123!",
            NewPassword = "NewPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Incorrect password");
    }

    [Fact]
    public async Task ChangePassword_WithWeakNewPassword_ShouldReturn400()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(TestAuthHandler.TestUserEmail);
        await userManager.AddPasswordAsync(user!, "Password123!");

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "Password123!",
            NewPassword = "weak"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Passwords must be at least");
    }
}
