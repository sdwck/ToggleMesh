using FastEndpoints;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.System.GetConfig;

public class GetConfigEndpoint : ToggleEndpoint<EmptyRequest, SystemConfigResponse>
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;

    public GetConfigEndpoint(IConfiguration configuration, AppDbContext db)
    {
        _configuration = configuration;
        _db = db;
    }

    public override void Configure()
    {
        Get("/system/config");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var allowOpenRegistration = _configuration.GetValue("Auth:AllowOpenRegistration", true);
        var allowUserOrganizationCreation = _configuration.GetValue("Auth:AllowUserOrganizationCreation", true);
        var passwordPolicy = new PasswordPolicyDto(
            _configuration.GetValue("Auth:PasswordPolicy:MinimumLength", 8),
            _configuration.GetValue("Auth:PasswordPolicy:RequireDigit", true),
            _configuration.GetValue("Auth:PasswordPolicy:RequireLowercase", true),
            _configuration.GetValue("Auth:PasswordPolicy:RequireUppercase", true),
            _configuration.GetValue("Auth:PasswordPolicy:RequireNonAlphanumeric", true)
        );

        if (!allowUserOrganizationCreation 
            && User.Identity?.IsAuthenticated == true 
            && User.TryGetUserId(out var userId))
        {
            var adminEmail = _configuration["DEFAULT_ADMIN_EMAIL"];
            var user = await _db.Users.FindAsync([userId], ct);
            if (user != null && string.Equals(user.Email, adminEmail, StringComparison.OrdinalIgnoreCase))
                allowUserOrganizationCreation = true;
        }

        await Send.OkAsync(new SystemConfigResponse(allowOpenRegistration, allowUserOrganizationCreation, passwordPolicy), cancellation: ct);
    }
}
