using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Auth.CreateToken;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Auth;

[Collection("SharedEnv3")]
public class PersonalAccessTokensTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public PersonalAccessTokensTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateToken_ShouldReturnPlainToken_AndSaveHashInDb()
    {
        // Arrange
        var request = new CreateTokenRequest { Name = "Global CLI Token", ExpiresInDays = 7 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/user/tokens", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CreateTokenResponse>();
        result.Should().NotBeNull();
        result.Name.Should().Be("Global CLI Token");
        result.PlainToken.Should().StartWith("tmp_");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenHash = ApiKeyHasher.Hash(result.PlainToken);
        var dbToken = await db.PersonalAccessTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        dbToken.Should().NotBeNull();
        dbToken.Name.Should().Be("Global CLI Token");
    }

    [Fact]
    public async Task AuthenticateWithPat_ShouldSucceed_AndUpdateLastUsedAt()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "PAT Auth Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });
        var env = new ProjectEnvironment { Name = "Prod_PAT", Project = project };
        db.Environments.Add(env);
        await db.SaveChangesAsync();

        var rawSecret = Guid.NewGuid().ToString("N");
        var plainToken = $"tmp_{rawSecret}";
        var tokenHash = ApiKeyHasher.Hash(plainToken);
        var pat = new PersonalAccessToken
        {
            Id = Guid.CreateVersion7(),
            UserId = Guid.Parse(TestAuthHandler.TestUserId),
            Name = "CLI Token Auth Test",
            TokenHash = tokenHash,
            TokenPreview = "tmp_prev"
        };
        db.PersonalAccessTokens.Add(pat);
        await db.SaveChangesAsync();

        pat.LastUsedAt.Should().BeNull();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sdk/flags");
        request.Headers.Add("x-pat-token", plainToken);
        request.Headers.Add("x-environment-id", env.Id.ToString());
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        db.ChangeTracker.Clear();
        var dbPat = await db.PersonalAccessTokens.FindAsync(pat.Id);
        dbPat!.LastUsedAt.Should().NotBeNull();
        dbPat.LastUsedAt.Value.Should().BeCloseTo(_factory.TimeProvider.GetUtcNow().UtcDateTime, TimeSpan.FromSeconds(5));
    }
}