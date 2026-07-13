using System.Data.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Respawn;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Data.Interceptors;
using ToggleMesh.API.Infrastructure.Email;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

using Npgsql;

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

    private Respawner _respawner = null!;
    private DbConnection _dbConnection = null!;

    public FakeTimeProvider TimeProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimits:Auth"] = "10000",
                ["RateLimits:Sdk"] = "10000"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestAuthStartupFilter>();

            var authConfigs = services.Where(d =>
                d.ServiceType.IsGenericType &&
                d.ServiceType.GetGenericArguments().Length > 0 &&
                d.ServiceType.GetGenericArguments()[0] == typeof(AuthenticationOptions))
                .ToList();
            foreach (var desc in authConfigs)
            {
                services.Remove(desc);
            }

            services.AddAuthentication(defaultScheme: TestAuthHandler.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.AuthenticationScheme, _ => { })
                .AddScheme<AuthenticationSchemeOptions, TestTempCookieHandler>(
                    "TempCookie", _ => { });
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(TimeProvider);

            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor is not null)
                services.Remove(redisDescriptor);

            var workersToRemove = services.Where(d => 
                d.ServiceType == typeof(IHostedService) && 
                ((d.ImplementationType?.Name != null && (d.ImplementationType.Name == "RollupWorker" || d.ImplementationType.Name == "AnomalyWorker" || d.ImplementationType.Name == "WebhookDispatcherService")) ||
                 (d.ImplementationFactory != null && (d.ImplementationFactory.ToString()!.Contains("RollupWorker") || d.ImplementationFactory.ToString()!.Contains("AnomalyWorker") || d.ImplementationFactory.ToString()!.Contains("WebhookDispatcherService"))))
            ).ToList();
            foreach (var w in workersToRemove)
                services.Remove(w);

            services.AddSingleton<SoftDeletableInterceptor>();
            services.AddSingleton<UpdateAuditableInterceptor>();
            services.AddSingleton<AuditInterceptor>();
            services.AddSingleton<RealTimeInvalidationInterceptor>();
            services.AddSingleton<TestOrganizationInterceptor>();
            var emailSenders = services.Where(d => d.ServiceType == typeof(IEmailSender)).ToList();
            foreach (var s in emailSenders)
                services.Remove(s);
            services.AddSingleton<IEmailSender, FakeEmailSender>();
            services.AddScoped<IEmailSender, DatabaseOutboxEmailSender>();

            var npgsqlDataSourceBuilder = new NpgsqlDataSourceBuilder(_db.GetConnectionString());
            npgsqlDataSourceBuilder.EnableDynamicJson();
            var npgsqlDataSource = npgsqlDataSourceBuilder.Build();

            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseNpgsql(npgsqlDataSource);
                options.AddInterceptors(
                    sp.GetRequiredService<SoftDeletableInterceptor>(),
                    sp.GetRequiredService<UpdateAuditableInterceptor>(),
                    sp.GetRequiredService<TestOrganizationInterceptor>(),
                    sp.GetRequiredService<RealTimeInvalidationInterceptor>());
            });

            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(_redis.GetConnectionString() + ",allowAdmin=true"));
        });
    }

    private static readonly Lock HostCreationLock = new();
    
    protected override IHost CreateHost(IHostBuilder builder)
    {
        lock (HostCreationLock)
        {
            return base.CreateHost(builder);
        }
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        await _redis.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _db.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("Analytics__ClickHouse__ConnectionString", " ");
        Environment.SetEnvironmentVariable("Analytics__Kafka__BootstrapServers", " ");

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _dbConnection = new NpgsqlConnection(_db.GetConnectionString());
        await _dbConnection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });

        await ResetDatabaseAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_dbConnection);

        var redis = Services.GetRequiredService<IConnectionMultiplexer>();
        var server = redis.GetServer(redis.GetEndPoints().First());
        await server.FlushDatabaseAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var testUser = new ApplicationUser
        {
            Id = Guid.Parse(TestAuthHandler.TestUserId),
            UserName = "test@example.com",
            Email = "test@example.com",
            NormalizedUserName = "TEST@EXAMPLE.COM",
            NormalizedEmail = "TEST@EXAMPLE.COM"
        };
        db.Users.Add(testUser);

        var testOrgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var testOrg = new Organization
        {
            Id = testOrgId,
            Name = "Test Organization",
            CreatedAt = DateTime.UtcNow
        };
        db.Organizations.Add(testOrg);

        var testOrgMember = new OrganizationMember
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = testOrgId,
            UserId = testUser.Id,
            Role = OrganizationRole.Admin
        };
        db.OrganizationMembers.Add(testOrgMember);
        await db.SaveChangesAsync();
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _redis.DisposeAsync();
        await base.DisposeAsync();
    }
}

public class TestOrganizationInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
{
    public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
        Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is AppDbContext dbContext)
        {
            SetTestOrganizationId(dbContext);
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> SavingChanges(
        Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result)
    {
        if (eventData.Context is AppDbContext dbContext)
        {
            SetTestOrganizationId(dbContext);
        }
        return base.SavingChanges(eventData, result);
    }

    private void SetTestOrganizationId(AppDbContext dbContext)
    {
        var targetOrgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        foreach (var entry in dbContext.ChangeTracker.Entries<Project>())
        {
            if (entry.State == EntityState.Added && entry.Entity.OrganizationId == Guid.Empty)
            {
                entry.Entity.OrganizationId = targetOrgId;
            }
        }
    }
}