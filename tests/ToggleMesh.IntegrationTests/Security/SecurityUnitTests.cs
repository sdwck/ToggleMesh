using System.Net;
using FluentAssertions;
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
}
