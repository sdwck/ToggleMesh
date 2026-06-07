using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.IntegrationTests.Infrastructure;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder("postgres:latest")
            .WithDatabase("togglemesh_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private readonly RedisContainer _redis =
        new RedisBuilder("redis:latest")
            .Build();

    public FakeTimeProvider TimeProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(defaultScheme: TestAuthHandler.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.AuthenticationScheme, options => { });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(TimeProvider);

            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            var redisDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor is not null)
                services.Remove(redisDescriptor);

            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseNpgsql(_db.GetConnectionString());
            });

            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(_redis.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        await _redis.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var testUser = new API.Features.Auth.Models.ApplicationUser
        {
            Id = Guid.Parse(TestAuthHandler.TestUserId),
            UserName = "test@example.com",
            Email = "test@example.com",
            NormalizedUserName = "TEST@EXAMPLE.COM",
            NormalizedEmail = "TEST@EXAMPLE.COM"
        };
        db.Users.Add(testUser);
        await db.SaveChangesAsync();
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _redis.DisposeAsync();
        await base.DisposeAsync();
    }
}
