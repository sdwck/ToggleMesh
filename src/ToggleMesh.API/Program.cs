using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ToggleMesh.API.Exceptions;
using ToggleMesh.API.Hubs;
using ToggleMesh.API.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddFastEndpoints();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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
app.MapHub<ToggleHub>("/hubs/toggle");

app.Run();