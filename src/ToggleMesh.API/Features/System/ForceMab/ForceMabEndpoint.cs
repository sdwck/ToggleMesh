using FastEndpoints;
using StackExchange.Redis;
using ToggleMesh.API.Features.Analytics.Ingest;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Streaming;

namespace ToggleMesh.API.Features.System.ForceMab;

public class ForceMabEndpoint : EndpointWithoutRequest
{
    private readonly IWebHostEnvironment _env;
    private readonly IServiceProvider _serviceProvider;

    public ForceMabEndpoint(IWebHostEnvironment env, IServiceProvider serviceProvider)
    {
        _env = env;
        _serviceProvider = serviceProvider;
    }

    public override void Configure()
    {
        Post("/dev/force-mab");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!_env.IsDevelopment())
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var queryEngine = scope.ServiceProvider.GetRequiredService<IAnalyticsQueryEngine>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var math = scope.ServiceProvider.GetRequiredService<BayesianMathService>();
        var mabShifter = scope.ServiceProvider.GetRequiredService<IMabTrafficShifterService>();

        await queryEngine.AggregateMetricsAsync(ct);
        await queryEngine.AggregateContextualMetricsAsync(ct);

        var notifyHandler = new NotifyFlagUpdatedCommandHandler(
            scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>(),
            scope.ServiceProvider.GetRequiredService<ICacheInvalidator>(),
            scope.ServiceProvider.GetRequiredService<IToggleEventPublisher>(),
            scope.ServiceProvider.GetRequiredService<ILogger<NotifyFlagUpdatedCommandHandler>>(),
            scope.ServiceProvider.GetRequiredService<IConfiguration>()
        );

        await mabShifter.ProcessMabTrafficShiftingAsync(db, math, notifyHandler, ct);
        await mabShifter.ProcessContextualBanditAutoSegmentationAsync(db, math, notifyHandler, ct);

        await Send.OkAsync(new { status = "success", message = "MAB metrics aggregated and weights shifted." }, ct);
    }
}
