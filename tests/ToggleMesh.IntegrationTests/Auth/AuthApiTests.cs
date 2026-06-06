using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ToggleMesh.API.Features.Auth.Endpoints;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Auth;

public class AuthApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthApiTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_ShouldSucceed()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = $"user_{Guid.NewGuid()}@example.com",
            Password = "Password123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnAccessAndRefreshToken()
    {
        // Arrange
        var email = $"user_{Guid.NewGuid()}@example.com";
        var password = "Password123!";
        
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password
        });

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result!.Token.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnError()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "WrongPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Refresh_WithValidTokens_ShouldReturnNewTokens()
    {
        // Arrange
        var email = $"user_{Guid.NewGuid()}@example.com";
        var password = "Password123!";
        
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        
        var refreshRequest = new RefreshRequest
        {
            Token = loginResult!.Token,
            RefreshToken = loginResult.RefreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshResult = await response.Content.ReadFromJsonAsync<LoginResponse>();
        
        refreshResult!.Token.Should().NotBeNullOrEmpty();
        refreshResult.RefreshToken.Should().NotBeNullOrEmpty();
        
        refreshResult.Token.Should().NotBe(loginResult.Token);
        refreshResult.RefreshToken.Should().NotBe(loginResult.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithInvalidRefreshToken_ShouldReturnError()
    {
        // Arrange
        var email = $"user_{Guid.NewGuid()}@example.com";
        var password = "Password123!";
        
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        
        var refreshRequest = new RefreshRequest
        {
            Token = loginResult!.Token,
            RefreshToken = "invalid-refresh-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}