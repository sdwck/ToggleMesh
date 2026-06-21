using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Sse;
using ToggleMesh.API.Persistence;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Memory;

namespace ToggleMesh.API.Features.Organizations.AcceptInvitation;

public class AcceptInvitationEndpoint : ToggleEndpoint<AcceptInvitationRequest, AcceptInvitationResponse>
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISseService _sseService;
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;

    public AcceptInvitationEndpoint(AppDbContext db, UserManager<ApplicationUser> userManager, ISseService sseService, IConnectionMultiplexer redis, IMemoryCache memoryCache)
    {
        _db = db;
        _userManager = userManager;
        _sseService = sseService;
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
    }

    public override void Configure()
    {
        Post("/organizations/invites/{Token}/accept");
    }

    public override async Task HandleAsync(AcceptInvitationRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(UserId.ToString());
        if (user == null)
            ThrowError("User not found.");

        var invite = await _db.OrganizationInvitations
            .FirstOrDefaultAsync(i => i.Token == req.Token, ct);

        if (invite == null)
            ThrowError("Invitation not found or has been revoked.");

        if (invite.ExpiresAt < DateTimeOffset.UtcNow)
            ThrowError("This invitation has expired.");

        if (!string.Equals(user.Email, invite.Email, StringComparison.OrdinalIgnoreCase))
            ThrowError($"This invitation is for {invite.Email}. You are logged in as {user.Email}.");

        var exists = await _db.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == invite.OrganizationId && m.UserId == user.Id, ct);

        if (!exists)
        {
            _db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = invite.OrganizationId,
                UserId = user.Id,
                Role = invite.Role
            });
        }

        _db.OrganizationInvitations.Remove(invite);
        await _db.SaveChangesAsync(ct);

        await _sseService.BroadcastAsync("invalidate", new { queryKey = new object[] { "organizations", invite.OrganizationId, "members" } });

        var projectIds = await _db.Projects
            .Where(p => p.OrganizationId == invite.OrganizationId)
            .Select(p => p.Id)
            .ToListAsync(ct);

        foreach (var pId in projectIds)
        {
            await _sseService.BroadcastAsync("invalidate", new { queryKey = new object[] { "projects", pId, "members" } });
            var cacheKey = $"project-member-state:{pId}:{user.Id}";
            await _redis.KeyDeleteAsync(cacheKey);
            _memoryCache.Remove(cacheKey);
        }

        await Send.OkAsync(new AcceptInvitationResponse { OrganizationId = invite.OrganizationId }, cancellation: ct);
    }
}
