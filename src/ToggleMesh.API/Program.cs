using System.Threading.Channels;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using StackExchange.Redis;
using ToggleMesh.API.BackgroundServices;
using ToggleMesh.API.Exceptions;
using ToggleMesh.API.Features.Metrics;
using ToggleMesh.API.Hubs;
using ToggleMesh.API.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddScoped<ToggleMesh.API.Features.Projects.IApiKeyCacheService, ToggleMesh.API.Features.Projects.ApiKeyCacheService>();
builder.Services.AddFastEndpoints();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
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