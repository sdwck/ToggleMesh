using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using ToggleMesh.API.Features.Organizations.AcceptInvitation;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using ToggleMesh.API.Infrastructure.Sse;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Projects;

[Collection("SharedEnv2")]
public class OrganizationInvitationsTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public OrganizationInvitationsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AcceptInvitation_Should_AddUserToOrg_And_InvalidateSse_For_OrgMembers()
    {
        // Arrange
        var inviteEmail = "invited-user@example.com";
        var inviteToken = Guid.NewGuid().ToString("N");

        Guid orgId;
        Guid userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var org = new Organization { Name = "Invite Target Org" };
            db.Organizations.Add(org);

            var invitedUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = inviteEmail,
                Email = inviteEmail,
                NormalizedUserName = inviteEmail.ToUpperInvariant(),
                NormalizedEmail = inviteEmail.ToUpperInvariant()
            };
            db.Users.Add(invitedUser);

            var invite = new OrganizationInvitation
            {
                Organization = org,
                Email = inviteEmail,
                Role = OrganizationRole.Member,
                Token = inviteToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
            };
            db.OrganizationInvitations.Add(invite);

            await db.SaveChangesAsync();
            orgId = org.Id;
            userId = invitedUser.Id;
        }

        var sseService = _factory.Services.GetRequiredService<ISseService>();
        var sseMessages = new List<(string Event, string Data)>();
        var sseCts = new CancellationTokenSource();

        sseService.CreateConnection(
            Guid.NewGuid(),
            (evt, data) =>
            {
                lock (sseMessages)
                {
                    sseMessages.Add((evt, data));
                }
                return Task.CompletedTask;
            },
            () => { },
            sseCts.Token);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/invites/{inviteToken}/accept")
        {
            Content = JsonContent.Create(new AcceptInvitationRequest { Token = inviteToken })
        };
        request.Headers.Add("x-test-user-id", userId.ToString());

        var response = await _client.SendAsync(request, sseCts.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AcceptInvitationResponse>(cancellationToken: sseCts.Token);
        body.Should().NotBeNull();
        body.OrganizationId.Should().Be(orgId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var exists = await db.OrganizationMembers.AnyAsync(m => m.OrganizationId == orgId && m.UserId == userId, cancellationToken: sseCts.Token);
            exists.Should().BeTrue("user should be added as a member in the organization");

            var inviteDeleted = await db.OrganizationInvitations.AnyAsync(i => i.Token == inviteToken, cancellationToken: sseCts.Token);
            inviteDeleted.Should().BeFalse("invitation should be consumed and deleted");
        }

        await Task.Delay(1000, sseCts.Token);
        lock (sseMessages)
        {
            sseMessages.Should().Contain(m => m.Event == "invalidate" && m.Data.Contains("organizations") && m.Data.Contains("members"));
        }

        await sseCts.CancelAsync();
    }

    [Fact]
    public async Task AcceptInvitation_WithExpiredToken_ShouldReturn400()
    {
        // Arrange
        var inviteEmail = "expired-invited-user@example.com";
        var inviteToken = Guid.NewGuid().ToString("N");

        Guid userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var org = new Organization { Name = "Expired Invite Org" };
            db.Organizations.Add(org);

            var invitedUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = inviteEmail,
                Email = inviteEmail,
                NormalizedUserName = inviteEmail.ToUpperInvariant(),
                NormalizedEmail = inviteEmail.ToUpperInvariant()
            };
            db.Users.Add(invitedUser);

            var invite = new OrganizationInvitation
            {
                Organization = org,
                Email = inviteEmail,
                Role = OrganizationRole.Member,
                Token = inviteToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            };
            db.OrganizationInvitations.Add(invite);

            await db.SaveChangesAsync();
            userId = invitedUser.Id;
        }

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/invites/{inviteToken}/accept")
        {
            Content = JsonContent.Create(new AcceptInvitationRequest { Token = inviteToken })
        };
        request.Headers.Add("x-test-user-id", userId.ToString());

        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SsoCallback_Should_CreateUser_If_NotExists_And_Generate_ExchangeTicket()
    {
        // Arrange
        var ssoEmail = "sso-new-user@example.com";

        // Act
        var ssoUrl = "/api/v1/auth/sso/callback-handler";
        var request = new HttpRequestMessage(HttpMethod.Get, ssoUrl);
        request.Headers.Add("x-test-temp-cookie-email", ssoEmail);

        var noRedirectClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await noRedirectClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var redirectUri = response.Headers.Location;
        redirectUri.Should().NotBeNull();
        redirectUri.Query.Should().Contain("ticket=");

        var queryParams = System.Web.HttpUtility.ParseQueryString(redirectUri.Query);
        var ticket = queryParams["ticket"];
        ticket.Should().NotBeNullOrEmpty();

        var redis = _factory.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var dataJson = await redis.StringGetAsync($"sso:ticket:{ticket}");
        dataJson.HasValue.Should().BeTrue("SSO exchange ticket must be temporarily saved in Redis");

        var ticketData = JsonSerializer.Deserialize<SsoTicketData>(dataJson.ToString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        ticketData.Should().NotBeNull();
        ticketData.AccessToken.Should().NotBeNullOrEmpty();
        ticketData.RefreshToken.Should().NotBeNullOrEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == ssoEmail);
        user.Should().NotBeNull("SSO user should be automatically provisioned if not exists");
    }

    private class SsoTicketData
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
