using FluentAssertions;
using ToggleMesh.Common.Contexts;
using ToggleMesh.SDK.Models;

namespace ToggleMesh.IntegrationTests.Sdk;

public class ToggleMeshUserTests
{
    private struct TestContext : IContextAccessor
    {
        public string Country { get; set; }
        public string Plan { get; set; }

        public bool TryGetValue(string key, out string? value)
        {
            switch (key)
            {
                case "Country":
                case "country":
                    value = Country;
                    return true;
                case "Plan":
                case "plan":
                    value = Plan;
                    return true;
                default:
                    value = null;
                    return false;
            }
        }
    }

    [Fact]
    public void Constructor_ShouldStoreIdentityAndContext()
    {
        var ctx = new TestContext { Country = "US", Plan = "Pro" };
        var user = new ToggleMeshUser<TestContext>("user_123", ctx);

        user.Identity.Should().Be("user_123");
        user.Context.Country.Should().Be("US");
        user.Context.Plan.Should().Be("Pro");
    }

    [Fact]
    public void Constructor_NullIdentity_ShouldDefaultToEmpty()
    {
        var ctx = new TestContext { Country = "DE" };
        var user = new ToggleMeshUser<TestContext>(null!, ctx);

        user.Identity.Should().BeEmpty();
    }

    [Fact]
    public void Struct_ShouldBeReadonly_AndValueType()
    {
        typeof(ToggleMeshUser<TestContext>).IsValueType.Should().BeTrue();
    }

    [Fact]
    public void Context_ShouldBeAccessibleViaIContextAccessor()
    {
        var ctx = new TestContext { Country = "JP", Plan = "Free" };
        var user = new ToggleMeshUser<TestContext>("user_456", ctx);

        user.Context.TryGetValue("Country", out var country).Should().BeTrue();
        country.Should().Be("JP");

        user.Context.TryGetValue("Plan", out var plan).Should().BeTrue();
        plan.Should().Be("Free");

        user.Context.TryGetValue("NonExistent", out _).Should().BeFalse();
    }

    [Fact]
    public void TwoUsers_WithSameData_ShouldBeEqual()
    {
        var ctx1 = new TestContext { Country = "UK", Plan = "Pro" };
        var ctx2 = new TestContext { Country = "UK", Plan = "Pro" };

        var user1 = new ToggleMeshUser<TestContext>("user_1", ctx1);
        var user2 = new ToggleMeshUser<TestContext>("user_1", ctx2);

        user1.Identity.Should().Be(user2.Identity);
        user1.Context.Country.Should().Be(user2.Context.Country);
    }
}
