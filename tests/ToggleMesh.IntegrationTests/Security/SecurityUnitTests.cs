using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ToggleMesh.API.Infrastructure.Security;

namespace ToggleMesh.IntegrationTests.Security;

public class SecurityUnitTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("172.16.5.5", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("169.254.10.10", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("::1", true)]
    [InlineData("fd00::1", true)]
    [InlineData("fc00::ffff", true)]
    [InlineData("::ffff:127.0.0.1", true)]
    [InlineData("::ffff:10.0.0.1", true)]
    [InlineData("::ffff:8.8.8.8", false)]
    public void IsPrivateOrLocal_ShouldDetectCorrectly(string ipString, bool expected)
    {
        // Arrange
        var ip = IPAddress.Parse(ipString);

        // Act
        var result = SsrfValidator.IsPrivateOrLocal(ip);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ApiKeyHasher_ShouldProduceDifferentHashes_WhenPepperChanges()
    {
        // Arrange
        var plainKey = "my-secret-key-123";
        var originalPepper = ApiKeyHasher.Pepper;

        try
        {
            // Act
            ApiKeyHasher.Pepper = "PepperA";
            var hashA = ApiKeyHasher.Hash(plainKey);

            ApiKeyHasher.Pepper = "PepperB";
            var hashB = ApiKeyHasher.Hash(plainKey);

            // Assert
            hashA.Should().NotBe(hashB);
        }
        finally
        {
            ApiKeyHasher.Pepper = originalPepper;
        }
    }

    private static void ResetRsaKeyCache()
    {
        var field = typeof(RsaKeyProvider).GetField("_key", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, null);
    }

    [Fact]
    public void RsaKeyProvider_ShouldParseKey_WhenInlineWithoutNewlines()
    {
        try
        {
            ResetRsaKeyCache();
            using var rsa = RSA.Create(2048);
            var realPem = rsa.ExportRSAPrivateKeyPem();
            var escapedPem = realPem.Replace("\r", "").Replace("\n", "");

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:PrivateKeyPem"] = escapedPem
                })
                .Build();

            var key = RsaKeyProvider.GetKey(config);
            key.Should().NotBeNull();
            key.KeySize.Should().Be(2048);
        }
        finally
        {
            ResetRsaKeyCache();
        }
    }

    [Fact]
    public void RsaKeyProvider_ShouldThrow_WhenKeyIsInvalid()
    {
        try
        {
            ResetRsaKeyCache();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:PrivateKeyPem"] = "-----BEGIN PRIVATE KEY-----\\nINVALID_DATA\\n-----END PRIVATE KEY-----"
                })
                .Build();

            var act = () => RsaKeyProvider.GetKey(config);
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*CRITICAL: Failed to parse JWT private key*");
        }
        finally
        {
            ResetRsaKeyCache();
        }
    }

    [Fact]
    public void RsaKeyProvider_ShouldThrow_WhenKeySizeIsLessThan2048()
    {
        try
        {
            ResetRsaKeyCache();
            using var rsa = RSA.Create(1024);
            var pem = rsa.ExportRSAPrivateKeyPem();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:PrivateKeyPem"] = pem
                })
                .Build();

            var act = () => RsaKeyProvider.GetKey(config);
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*CRITICAL: Insufficient RSA Key Size*");
        }
        finally
        {
            ResetRsaKeyCache();
        }
    }
}
