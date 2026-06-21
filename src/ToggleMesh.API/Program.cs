using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.Channels;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using StackExchange.Redis;
using ToggleMesh.API.BackgroundServices;
using ToggleMesh.API.BackgroundServices.Auth;
using ToggleMesh.API.BackgroundServices.Caching;
using ToggleMesh.API.BackgroundServices.Metrics;
using ToggleMesh.API.BackgroundServices.Webhooks;
using ToggleMesh.API.Exceptions;
using ToggleMesh.API.Features.Auth.Authorization;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Features.Client;
using ToggleMesh.API.Features.Metrics;
using ToggleMesh.API.Features.Webhooks;
using ToggleMesh.API.Hubs;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Email;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Infrastructure.Sse;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Persistence.Interceptors;
using ToggleMesh.API.Persistence.Interceptors.Audit;
using ToggleMesh.Common.Rules;
using ToggleMesh.Common.Rules.Operators;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!);

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
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddScoped<IEmailSender, DatabaseOutboxEmailSender>();
builder.Services.AddHostedService<ToggleMesh.API.BackgroundServices.Email.EmailOutboxWorker>();

builder.Services.AddScoped<IApiKeyCacheService, ApiKeyCacheService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddFastEndpoints();
builder.Services.AddSingleton<SoftDeletableInterceptor>();
builder.Services.AddSingleton<UpdateAuditableInterceptor>();
builder.Services.AddSingleton<AuditInterceptor>();
builder.Services.AddSingleton<RealTimeInvalidationInterceptor>();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(
        serviceProvider.GetRequiredService<SoftDeletableInterceptor>(),
        serviceProvider.GetRequiredService<UpdateAuditableInterceptor>(),
        serviceProvider.GetRequiredService<AuditInterceptor>(),
        serviceProvider.GetRequiredService<RealTimeInvalidationInterceptor>());
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHostedService<MetricsWorker>();
builder.Services.AddSingleton(Channel.CreateBounded<MetricQueueItem>(new BoundedChannelOptions(100000)
{
    FullMode = BoundedChannelFullMode.DropOldest
}));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
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
        policy.WithOrigins("http://localhost:5173")
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
    
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = authLimit;
        o.QueueLimit = 0;
    });

    options.AddSlidingWindowLimiter("sdk", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.SegmentsPerWindow = 4;
        o.PermitLimit = sdkLimit;
        o.QueueLimit = 0;
    });
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<ICacheInvalidator, RedisCacheInvalidator>();
builder.Services.AddSingleton<ISseService, SseService>();
builder.Services.AddHostedService<CacheInvalidationWorker>();
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
});

builder.Services.AddSingleton(Channel.CreateBounded<WebhookEvent>(
    new BoundedChannelOptions(100_000)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    }));

builder.Services.AddHostedService<WebhookDispatcherService>();
builder.Services.AddHostedService<WebhookDeliveryWorker>();
builder.Services.AddHostedService<WebhookCleanupWorker>();
builder.Services.AddHostedService<RefreshTokenCleanupService>();

var app = builder.Build();

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

app.UseHttpsRedirection();
app.UseCors("AdminUI");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api";
    c.Versioning.Prefix = "v";
    c.Versioning.DefaultVersion = 1;
    c.Versioning.PrependToRoute = true;
});
app.MapHub<ToggleHub>("/api/v1/hubs/toggle");

app.Run();