using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Email;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Auth.Endpoints.Register;

public class RegisterEndpoint : ToggleEndpoint<RegisterRequest, RegisterResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IEmailTemplateService _templateService;

    public RegisterEndpoint(UserManager<ApplicationUser> userManager, IEmailSender emailSender, IConfiguration configuration, AppDbContext db, TimeProvider timeProvider, IEmailTemplateService templateService)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _configuration = configuration;
        _db = db;
        _timeProvider = timeProvider;
        _templateService = templateService;
    }

    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        var allowOpenRegistration = _configuration.GetValue("Auth:AllowOpenRegistration", true);
        
        var isInviteValid = false;
        if (!string.IsNullOrWhiteSpace(req.InviteToken))
        {
            var invite = await _db.OrganizationInvitations
                .FirstOrDefaultAsync(i => i.Token == req.InviteToken && i.Email == req.Email, ct);
            if (invite != null)
            {
                isInviteValid = true;
            }
        }

        if (!allowOpenRegistration && !isInviteValid)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            EmailConfirmed = isInviteValid
        };

        var result = await _userManager.CreateAsync(user, req.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                AddError(error.Description);
            ThrowIfAnyErrors();
        }

        if (!isInviteValid)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var frontendUrl = _configuration["Auth:FrontendUrl"] ?? "http://localhost:5173";
            var confirmUrl = $"{frontendUrl}/auth/confirm-email?userId={user.Id}&token={encodedToken}";

            var startYear = 2026;
            var currentYear = _timeProvider.GetUtcNow().Year;
            var copyrightYear = currentYear > startYear ? $"{startYear}-{currentYear}" : startYear.ToString();

            var emailBody = await _templateService.RenderAsync("ConfirmEmailTemplate", new 
            { 
                ConfirmUrl = confirmUrl,
                ToggleMeshLogoUrl = "https://raw.githubusercontent.com/sdwck/ToggleMesh/main/src/ToggleMesh.AdminUI/src/assets/icon.png",
                CopyrightYear = copyrightYear,
                DashboardUrl = frontendUrl
            }, ct);

            await _emailSender.SendEmailAsync(user.Email, "Confirm your ToggleMesh account", emailBody, ct);
            await Send.OkAsync(new RegisterResponse(), cancellation: ct);
            return;
        }

        var (accessToken, refreshToken) = await TokenGenerator.GenerateTokensAsync(user, _userManager, _configuration, _timeProvider);

        if (!int.TryParse(_configuration["Auth:RefreshTokenLifetimeDays"], out var tokenLifetime))
            tokenLifetime = 7;
        
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var rt = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            Expires = now.AddDays(tokenLifetime),
            Created = now
        };
        
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync(ct);

        await Send.OkAsync(new RegisterResponse
        {
            Token = accessToken,
            RefreshToken = refreshToken
        }, cancellation: ct);
    }
}