using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using ToggleMesh.API.Infrastructure.Email;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Auth.Register;

public class ResendConfirmationEmailEndpoint : ToggleEndpoint<ResendConfirmationEmailRequest>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templateService;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public ResendConfirmationEmailEndpoint(
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
        Post("/auth/resend-confirmation");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(ResendConfirmationEmailRequest req, CancellationToken ct)
    {
        var enableEmails = _configuration.GetValue("Email:EnableEmails", false);
        if (!enableEmails)
        {
            await Send.OkAsync(cancellation: ct);
            return;
        }

        var user = await _userManager.FindByEmailAsync(req.Email);
        
        if (user == null || user.EmailVerificationMethod == EmailVerificationMethod.Email || user.EmailVerificationMethod == EmailVerificationMethod.Admin || user.EmailVerificationMethod == EmailVerificationMethod.Sso)
        {
            await Send.OkAsync(cancellation: ct);
            return;
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var appUrl = _configuration["Auth:AppUrl"] ?? "http://localhost:5264";
        var confirmUrl = $"{appUrl}/auth/confirm-email?userId={user.Id}&token={encodedToken}";

        var startYear = 2026;
        var currentYear = _timeProvider.GetUtcNow().Year;
        var copyrightYear = currentYear > startYear ? $"{startYear}-{currentYear}" : startYear.ToString();

        var emailBody = await _templateService.RenderAsync("ConfirmEmailTemplate", new 
        { 
            ConfirmUrl = confirmUrl,
            ToggleMeshLogoUrl = "https://raw.githubusercontent.com/sdwck/ToggleMesh/main/docs/assets/icon.png",
            CopyrightYear = copyrightYear,
            DashboardUrl = appUrl
        }, ct);

        await _emailSender.SendEmailAsync(user.Email!, "Confirm your ToggleMesh account", emailBody, ct);
        
        await Send.OkAsync(cancellation: ct);
    }
}
