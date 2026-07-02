using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public interface IMabTrafficShifterService
{
    Task ProcessMabTrafficShiftingAsync(
        AppDbContext db,
        BayesianMathService math,
        NotifyFlagUpdatedCommandHandler notifyHandler,
        CancellationToken ct);

    Task ProcessContextualBanditAutoSegmentationAsync(
        AppDbContext db,
        BayesianMathService math,
        NotifyFlagUpdatedCommandHandler notifyHandler,
        CancellationToken ct);
}
