using System.IdentityModel.Tokens.Jwt;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using StackExchange.Redis;
using ToggleMesh.API.Exceptions;
using ToggleMesh.API.Features.Analytics.Ingest;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Client.Domain;
using ToggleMesh.API.Features.Metrics.Domain;
using ToggleMesh.API.Features.Metrics.Workers;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Features.Webhooks.Workers;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.BackgroundServices.Caching;
using ToggleMesh.API.Infrastructure.BackgroundServices.Database;
using ToggleMesh.API.Infrastructure.BackgroundServices.Email;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Data.Interceptors;
using ToggleMesh.API.Infrastructure.Data.Interceptors.Audit;
using ToggleMesh.API.Infrastructure.Email;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Infrastructure.Security.Authorization;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using ToggleMesh.API.Infrastructure.Security.Authorization.Workers;
using ToggleMesh.API.Infrastructure.Sse;
using ToggleMesh.API.Infrastructure.Streaming;
using ToggleMesh.Common.Rules;
using ToggleMesh.Common.Rules.Operators;
using ToggleMesh.API.Features.Flags.Experiments.Stop;


var builder = WebApplication.CreateBuilder(args);
ApiKeyHasher.Pepper = builder.Configuration["Security:ApiKeyPepper"] ?? "DefaultToggleMeshPepperSecret123!";
builder.Services.AddOpenApi();
builder.Services.AddSingleton<ISseConnectionManager, SseConnectionManager>();
builder.Services.AddSingleton<IAesEncryptionService, AesEncryptionService>();
builder.Services.AddSingleton<IToggleEventPublisher, RedisToggleEventPublisher>();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddSingleton<IRuleOperator, ContainsOperator>();
builder.Services.AddSingleton<IRuleOperator, DateAfterOperator>();
builder.Services.AddSingleton<IRuleOperator, DateBeforeOperator>();
builder.Services.AddSingleton<IRuleOperator, EndsWithOperator>();
builder.Services.AddSingleton<IRuleOperator, EqualsOperator>();
builder.Services.AddSingleton<IRuleOperator, GreaterThanOperator>();
builder.Services.AddSingleton<IRuleOperator, GreaterThanOrEqualOperator>();
builder.Services.AddSingleton<IRuleOperator, InListOperator>();
builder.Services.AddSingleton<IRuleOperator, LessThanOperator>();
builder.Services.AddSingleton<IRuleOperator, LessThanOrEqualOperator>();
builder.Services.AddSingleton<IRuleOperator, NotEqualsOperator>();
builder.Services.AddSingleton<IRuleOperator, RegexOperator>();
builder.Services.AddSingleton<IRuleOperator, SemVerEqualOperator>();
builder.Services.AddSingleton<IRuleOperator, SemVerGreaterThanOperator>();
builder.Services.AddSingleton<IRuleOperator, SemVerGreaterThanOrEqualOperator>();
builder.Services.AddSingleton<IRuleOperator, SemVerLessThanOperator>();
builder.Services.AddSingleton<IRuleOperator, SemVerLessThanOrEqualOperator>();
builder.Services.AddSingleton<IRuleOperator, StartsWithOperator>();
builder.Services.AddSingleton<IRuleEngine, RuleEngine>();
builder.Services.AddScoped<ISdkEvaluatorService, SdkEvaluatorService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailSender, DatabaseOutboxEmailSender>();
builder.Services.AddSingleton<IEmailTemplateService, ScribanEmailTemplateService>();

builder.Services.AddScoped<IApiKeyCacheService, ApiKeyCacheService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddFastEndpoints();
builder.Services.AddSingleton<SoftDeletableInterceptor>();
builder.Services.AddSingleton<UpdateAuditableInterceptor>();
builder.Services.AddSingleton<AuditInterceptor>();
builder.Services.AddSingleton<RealTimeInvalidationInterceptor>();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Database connection string is missing.");

var npgsqlDataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
npgsqlDataSourceBuilder.EnableDynamicJson();
var npgsqlDataSource = npgsqlDataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(npgsqlDataSource);
    options.AddInterceptors(
        serviceProvider.GetRequiredService<SoftDeletableInterceptor>(),
        serviceProvider.GetRequiredService<UpdateAuditableInterceptor>(),
        serviceProvider.GetRequiredService<AuditInterceptor>(),
        serviceProvider.GetRequiredService<RealTimeInvalidationInterceptor>());
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(Channel.CreateBounded<MetricQueueItem>(new BoundedChannelOptions(100000)
{
    FullMode = BoundedChannelFullMode.DropOldest
}));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequireDigit = builder.Configuration.GetValue("Auth:PasswordPolicy:RequireDigit", true);
        options.Password.RequireLowercase = builder.Configuration.GetValue("Auth:PasswordPolicy:RequireLowercase", true);
        options.Password.RequireNonAlphanumeric = builder.Configuration.GetValue("Auth:PasswordPolicy:RequireNonAlphanumeric", true);
        options.Password.RequireUppercase = builder.Configuration.GetValue("Auth:PasswordPolicy:RequireUppercase", true);
        options.Password.RequiredLength = builder.Configuration.GetValue("Auth:PasswordPolicy:MinimumLength", 8);
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "MixedAuth";
        options.DefaultChallengeScheme = "MixedAuth";
    })
    .AddPolicyScheme("MixedAuth", "JWT or PAT", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Headers.ContainsKey("x-pat-token"))
                return "PAT";
            
            return JwtBearerDefaults.AuthenticationScheme;
        };
    })
    .AddScheme<AuthenticationSchemeOptions, PatAuthenticationHandler>("PAT", _ => { })
    .AddCookie("TempCookie");

var oidcSettings = builder.Configuration.GetSection("OIDC");
if (!string.IsNullOrEmpty(oidcSettings["ClientId"]))
{
    builder.Services.AddAuthentication()
        .AddOpenIdConnect(options =>
        {
            options.SignInScheme = "TempCookie";
            options.Authority = oidcSettings["Authority"];
            options.ClientId = oidcSettings["ClientId"];
            options.ClientSecret = oidcSettings["ClientSecret"];
            options.ResponseType = "code";
            options.SaveTokens = true;
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.CallbackPath = "/api/v1/auth/sso/callback";
        });
}

builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        var jwtSettings = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role",
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = RsaKeyProvider.GetKey(builder.Configuration)
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminUI", policy =>
    {
        var frontendUrl = builder.Configuration["Auth:FrontendUrl"] ?? "http://localhost:5173";
        policy.WithOrigins(frontendUrl)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
    
    options.AddPolicy("PublicSdk", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    
    var authLimit = builder.Configuration.GetValue("RateLimits:Auth", 10);
    var sdkLimit = builder.Configuration.GetValue("RateLimits:Sdk", 1000);
    
    options.AddPolicy("auth", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (authLimit <= 0) 
            return RateLimitPartition.GetNoLimiter(ip);
        
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddPolicy("sdk", context =>
    {
        var apiKey = context.Request.Headers["x-api-key"].ToString();
        var partitionKey = string.IsNullOrEmpty(apiKey) ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown" : apiKey;

        if (sdkLimit <= 0) 
            return RateLimitPartition.GetNoLimiter(partitionKey);

        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = sdkLimit,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 4,
            QueueLimit = 0
        });
    });
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<ICacheInvalidator, RedisCacheInvalidator>();
builder.Services.AddSingleton<ISseService, SseService>();
builder.Services.Scan(x =>
    x.FromAssemblies(typeof(IAuditAnalyzer).Assembly)
        .AddClasses(xx => xx.AssignableTo<IAuditAnalyzer>())
        .AsImplementedInterfaces()
        .WithSingletonLifetime());
builder.Services.Scan(x =>
    x.FromAssemblies(typeof(ICacheInvalidationHandler).Assembly)
        .AddClasses(xx => xx.AssignableTo<ICacheInvalidationHandler>())
        .AsImplementedInterfaces()
        .WithSingletonLifetime());
builder.Services.AddHttpClient("WebhookClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new SocketsHttpHandler
    {
        ConnectCallback = async (context, ct) =>
        {
            var host = context.DnsEndPoint.Host;
            var addresses = await System.Net.Dns.GetHostAddressesAsync(host, ct);

            foreach (var ip in addresses)
            {
                if (SsrfValidator.IsPrivateOrLocal(ip))
                {
                    throw new System.Security.SecurityException($"DNS Rebinding Protection: IP {ip} is private or local.");
                }
            }

            var targetIp = addresses[0];
            var socket = new System.Net.Sockets.Socket(targetIp.AddressFamily, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            try
            {
                socket.NoDelay = true;
                await socket.ConnectAsync(new System.Net.IPEndPoint(targetIp, context.DnsEndPoint.Port), ct);
                return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };
});

builder.Services.AddSingleton(Channel.CreateBounded<WebhookEvent>(
    new BoundedChannelOptions(100_000)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    }));

builder.Services.AddHostedService<CacheInvalidationWorker>();
builder.Services.AddHostedService<WebhookCleanupWorker>();
builder.Services.AddHostedService<PartitioningWorker>();
builder.Services.AddHostedService<SseRedisSubscriber>();
builder.Services.AddHostedService<RefreshTokenCleanupService>();
builder.Services.AddHostedService<EmailOutboxWorker>();
builder.Services.AddHostedService<MetricsWorker>();
builder.Services.AddHostedService<AnomalyWorker>();
builder.Services.AddHostedService<WebhookDispatcherService>();
builder.Services.AddHostedService<WebhookDeliveryWorker>();
builder.Services.AddHostedService<RollupWorker>();

var kafkaServers = builder.Configuration["Analytics:Kafka:BootstrapServers"];
var clickHouseConn = builder.Configuration["Analytics:ClickHouse:ConnectionString"];

if (!string.IsNullOrWhiteSpace(kafkaServers))
{
    builder.Services.AddSingleton<IAnalyticsEventPublisher, KafkaAnalyticsPublisher>();
    builder.Services.AddHostedService<KafkaAnalyticsConsumerWorker>();
}
else
{
    builder.Services.AddSingleton<InMemoryAnalyticsQueue>();
    builder.Services.AddSingleton<IAnalyticsEventPublisher>(sp => sp.GetRequiredService<InMemoryAnalyticsQueue>());
    builder.Services.AddHostedService<AnalyticsWorker>();
}

if (!string.IsNullOrWhiteSpace(clickHouseConn))
{
    builder.Services.AddSingleton<IAnalyticsStorageSink, ClickHouseAnalyticsSink>();
    builder.Services.AddScoped<IAnalyticsQueryEngine, ClickHouseQueryEngine>();
}
else
{
    builder.Services.AddSingleton<IAnalyticsStorageSink, PostgresAnalyticsSink>();
    builder.Services.AddScoped<IAnalyticsQueryEngine, PostgresQueryEngine>();
}

builder.Services.AddSingleton<BayesianMathService>();
builder.Services.AddScoped<ExperimentSnapshotBuilder>();
builder.Services.AddScoped<IMabTrafficShifterService, MabTrafficShifterService>();

var app = builder.Build();

await app.ApplyMigrationsAndSeedAsync();

app.UseExceptionHandler();

if (app.Environment.IsStaging() || app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-XSS-Protection"] = "0";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

if (app.Environment.IsStaging() || app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseCors("AdminUI");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference("/docs");

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api";
    c.Versioning.Prefix = "v";
    c.Versioning.DefaultVersion = 1;
    c.Versioning.PrependToRoute = true;
});
app.Run();