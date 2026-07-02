using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Infrastructure.Email;

namespace ToggleMesh.IntegrationTests.Infrastructure;

[Collection("SharedEnv1")]
public class EmailTemplateTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private IEmailTemplateService _templateService = null!;

    public EmailTemplateTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        var scope = _factory.Services.CreateScope();
        _templateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RenderAsync_InviteTemplate_ShouldRenderCorrectly()
    {
        var html = await _templateService.RenderAsync("InviteTemplate", new
        {
            OrganizationName = "Test Org",
            InviteUrl = "https://test.com/invite/123",
            ToggleMeshLogoUrl = "https://test.com/logo.png",
            CopyrightYear = "2026"
        });

        html.Should().Contain("Test Org");
        html.Should().Contain("https://test.com/invite/123");
        html.Should().Contain("https://test.com/logo.png");
        html.Should().Contain("2026");
        html.Should().Contain("<html");
    }

    [Fact]
    public async Task RenderAsync_ConfirmEmailTemplate_ShouldRenderCorrectly()
    {
        var html = await _templateService.RenderAsync("ConfirmEmailTemplate", new
        {
            ConfirmUrl = "https://test.com/confirm/123",
            ToggleMeshLogoUrl = "https://test.com/logo.png",
            CopyrightYear = "2026"
        });

        html.Should().Contain("https://test.com/confirm/123");
        html.Should().Contain("https://test.com/logo.png");
        html.Should().Contain("2026");
        html.Should().Contain("<html");
    }

    [Fact]
    public async Task RenderAsync_ForgotPasswordTemplate_ShouldRenderCorrectly()
    {
        var html = await _templateService.RenderAsync("ForgotPasswordTemplate", new
        {
            ResetUrl = "https://test.com/reset/123",
            ToggleMeshLogoUrl = "https://test.com/logo.png",
            CopyrightYear = "2026"
        });

        html.Should().Contain("https://test.com/reset/123");
        html.Should().Contain("https://test.com/logo.png");
        html.Should().Contain("2026");
        html.Should().Contain("<html");
    }
}
