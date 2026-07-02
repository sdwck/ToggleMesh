using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using ToggleMesh.API.Infrastructure.Email;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.ForgotPassword;

public class ForgotPasswordEndpoint : ToggleEndpoint<ForgotPasswordRequest>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templateService;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public ForgotPasswordEndpoint(
        UserManager<ApplicationUser> userManager, 
        IEmailSender emailSender, 
        IEmailTemplateService templateService, 
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _templateService = templateService;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public override void Configure()
    {
        Post("/auth/forgot-password");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(ForgotPasswordRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        
        if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
        {
            await Send.OkAsync(cancellation: ct);
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var frontendUrl = _configuration["Auth:FrontendUrl"] ?? "http://localhost:5173";
        var resetUrl = $"{frontendUrl}/auth/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={encodedToken}";

        var startYear = 2026;
        var currentYear = _timeProvider.GetUtcNow().Year;
        var copyrightYear = currentYear > startYear ? $"{startYear}-{currentYear}" : startYear.ToString();

        var emailBody = await _templateService.RenderAsync("ForgotPasswordTemplate", new 
        { 
            ResetUrl = resetUrl,
            ToggleMeshLogoUrl = "https://raw.githubusercontent.com/sdwck/ToggleMesh/main/src/ToggleMesh.AdminUI/src/assets/icon.png",
            CopyrightYear = copyrightYear,
            DashboardUrl = frontendUrl
        }, ct);

        await _emailSender.SendEmailAsync(user.Email!, "Reset your ToggleMesh password", emailBody, ct);

        await Send.OkAsync(cancellation: ct);
    }
}
