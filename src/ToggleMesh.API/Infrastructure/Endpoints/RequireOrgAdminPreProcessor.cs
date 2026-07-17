using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Infrastructure.Endpoints;

public class RequireOrgAdminPreProcessor<TRequest> : IPreProcessor<TRequest> 
    where TRequest : notnull
{
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        var orgIdStr = context.HttpContext.Request.RouteValues["OrganizationId"]?.ToString()
                       ?? context.HttpContext.Request.RouteValues["organizationId"]?.ToString();
                       
        if (!Guid.TryParse(orgIdStr, out var orgId)) 
            return;

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        
        var userId = context.HttpContext.User.GetUserId();

        var member = await db.OrganizationMembers
            .AsNoTracking()
            .Include(m => m.Organization)
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == userId && m.Role == OrganizationRole.Admin, ct);

        if (member == null)
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);
            context.ValidationFailures.Add(new FluentValidation.Results.ValidationFailure("Role", "Organization Admin required"));
            return;
        }

        if (member.Organization.RequireTwoFactor)
        {
            var hasMfaClaim = context.HttpContext.User.Claims.Any(c => c.Type == "amr" && c.Value == "mfa");
            if (!hasMfaClaim)
            {
                context.HttpContext.Response.Headers.Append("X-Requires-TwoFactor", "true");
                await context.HttpContext.Response.SendForbiddenAsync(ct);
                context.ValidationFailures.Add(new FluentValidation.Results.ValidationFailure("2FA", "TwoFactorRequired"));
                return;
            }
        }
    }
}
