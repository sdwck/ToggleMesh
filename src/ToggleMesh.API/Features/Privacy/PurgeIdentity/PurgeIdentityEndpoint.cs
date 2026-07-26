using ClickHouse.Client.ADO;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Privacy.PurgeIdentity;

public class PurgeIdentityEndpoint : ToggleEndpoint<PurgeIdentityRequest, PurgeIdentityResponse>
{
    private readonly AppDbContext _db;
    private readonly IIdentityHasher _identityHasher;
    private readonly IConfiguration _configuration;

    public PurgeIdentityEndpoint(AppDbContext db, IIdentityHasher identityHasher, IConfiguration configuration)
    {
        _db = db;
        _identityHasher = identityHasher;
        _configuration = configuration;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/privacy/purge-identity");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.ProjectsEdit);
    }

    public override async Task HandleAsync(PurgeIdentityRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");

        if (string.IsNullOrWhiteSpace(req.Identity))
        {
            AddError("Identity cannot be empty.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var hashedIdentity = _identityHasher.HashIdentity(req.Identity);

        var projectEnvs = await _db.Environments
            .Where(e => e.ProjectId == projectId)
            .Select(e => e.Id)
            .ToListAsync(ct);

        var exposuresQuery = _db.AnalyticsExposures.Where(e => projectEnvs.Contains(e.EnvironmentId) && (e.Identity == hashedIdentity || e.Identity == req.Identity));
        var tracksQuery = _db.AnalyticsTracks.Where(t => projectEnvs.Contains(t.EnvironmentId) && (t.Identity == hashedIdentity || t.Identity == req.Identity));

        if (req.EnvironmentId.HasValue)
        {
            exposuresQuery = exposuresQuery.Where(e => e.EnvironmentId == req.EnvironmentId.Value);
            tracksQuery = tracksQuery.Where(t => t.EnvironmentId == req.EnvironmentId.Value);
        }

        var exposuresPurged = await exposuresQuery.ExecuteDeleteAsync(ct);
        var tracksPurged = await tracksQuery.ExecuteDeleteAsync(ct);

        var chConnectionString = _configuration["Analytics:ClickHouse:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(chConnectionString))
        {
            try
            {
                await using var chConn = new ClickHouseConnection(chConnectionString);
                await chConn.OpenAsync(ct);

                var envsList = string.Join("','", projectEnvs);
                var projectEnvClause = $"EnvironmentId IN ('{envsList}')";
                var specificEnvClause = req.EnvironmentId.HasValue ? $" AND EnvironmentId = '{req.EnvironmentId.Value}'" : "";
                
                var chExposureSql = $"ALTER TABLE AnalyticsExposures DELETE WHERE (Identity = '{hashedIdentity}' OR Identity = '{req.Identity.Replace("'", "''")}') AND {projectEnvClause}{specificEnvClause}";
                var chTrackSql = $"ALTER TABLE AnalyticsTracks DELETE WHERE (Identity = '{hashedIdentity}' OR Identity = '{req.Identity.Replace("'", "''")}') AND {projectEnvClause}{specificEnvClause}";

                await using var cmd1 = chConn.CreateCommand();
                cmd1.CommandText = chExposureSql;
                await cmd1.ExecuteNonQueryAsync(ct);

                await using var cmd2 = chConn.CreateCommand();
                cmd2.CommandText = chTrackSql;
                await cmd2.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PurgeIdentityEndpoint] Failed to purge records from ClickHouse");
            }
        }

        await Send.OkAsync(new PurgeIdentityResponse
        {
            ExposuresPurged = exposuresPurged,
            TracksPurged = tracksPurged,
            ExecutedAt = DateTimeOffset.UtcNow
        }, ct);
    }
}
