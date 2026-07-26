using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ToggleMesh.API.Features.Analytics.Services;

namespace ToggleMesh.IntegrationTests.Analytics;

public class GdprComplianceTests
{
    [Fact]
    public void IdentityHasher_ShouldDeterministicallyHashIdentities()
    {
        var config = new ConfigurationBuilder().Build();
        var hasher = new IdentityHasher(config, NullLogger<IdentityHasher>.Instance);

        var hashed1 = hasher.HashIdentity("user_123");
        var hashed2 = hasher.HashIdentity("user_123");
        var hashed3 = hasher.HashIdentity("user_456");

        hashed1.Should().NotBeNullOrEmpty();
        hashed1.Length.Should().Be(32);
        hashed1.Should().Be(hashed2);
        hashed1.Should().NotBe(hashed3);
        hashed1.Should().NotBe("user_123");
    }

    [Fact]
    public void IdentityHasher_FormatLiveTailIdentity_ShouldTruncateHash()
    {
        var config = new ConfigurationBuilder().Build();
        var hasher = new IdentityHasher(config, NullLogger<IdentityHasher>.Instance);

        var formatted = hasher.FormatLiveTailIdentity("user_123");

        formatted.Should().EndWith("...");
        formatted.Length.Should().Be(11);
    }

    [Fact]
    public void PropertySanitizer_ShouldRedactPiiKeys_AndPreserveNormalKeys()
    {
        var sanitizer = new PropertySanitizer();
        var rawProps = new
        {
            email = "john@company.com",
            password = "supersecretpass",
            credit_card = "1234-5678-9012-3456",
            country = "US",
            tier = "enterprise"
        };

        var sanitized = sanitizer.Sanitize(rawProps);

        sanitized.Should().NotBeNull();
        var json = JsonSerializer.Serialize(sanitized);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("email").GetString().Should().Be("[REDACTED_PII]");
        root.GetProperty("password").GetString().Should().Be("[REDACTED_PII]");
        root.GetProperty("credit_card").GetString().Should().Be("[REDACTED_PII]");
        root.GetProperty("country").GetString().Should().Be("US");
        root.GetProperty("tier").GetString().Should().Be("enterprise");
    }

    [Fact]
    public void PropertySanitizer_ShouldReturnOriginal_WhenNoPiiKeysPresent()
    {
        var sanitizer = new PropertySanitizer();
        var rawProps = new
        {
            country = "GB",
            browser = "Chrome",
            version = 120
        };

        var sanitized = sanitizer.Sanitize(rawProps);

        sanitized.Should().BeSameAs(rawProps);
    }
}
