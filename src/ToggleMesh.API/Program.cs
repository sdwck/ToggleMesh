using System.Threading.Channels;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using StackExchange.Redis;
using ToggleMesh.API.BackgroundServices;
using ToggleMesh.API.Exceptions;
using ToggleMesh.API.Features.Metrics;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Hubs;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Persistence.Interceptors;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => 
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddScoped<IApiKeyCacheService, ApiKeyCacheService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddFastEndpoints();
builder.Services.AddSingleton<AuditInterceptor>();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(
        serviceProvider.GetRequiredService<AuditInterceptor>());
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHostedService<MetricsWorker>();
builder.Services.AddSingleton(Channel.CreateBounded<MetricQueueItem>(new BoundedChannelOptions(100000)
{
    FullMode = BoundedChannelFullMode.DropOldest
}));

var app = builder.Build();

app.UseHttpsRedirection();

if (app.Environment.IsStaging() || app.Environment.IsProduction()) 
{
    app.UseExceptionHandler();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseFastEndpoints();
app.MapHub<ToggleHub>("/api/hubs/toggle");

app.Run();