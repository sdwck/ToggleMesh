using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Email;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ToggleMesh.API.Features.Auth.Endpoints.Register;

public class RegisterEndpoint : ToggleEndpoint<RegisterRequest, RegisterResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;

    public RegisterEndpoint(UserManager<ApplicationUser> userManager, IEmailSender emailSender, IConfiguration configuration, AppDbContext db)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _configuration = configuration;
        _db = db;
    }

    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();
        Options(x => x.RequireRateLimiting("auth"));
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        bool isInviteValid = false;
        if (!string.IsNullOrWhiteSpace(req.InviteToken))
        {
            var invite = await _db.OrganizationInvitations
                .FirstOrDefaultAsync(i => i.Token == req.InviteToken && i.Email == req.Email, ct);
            if (invite != null)
            {
                isInviteValid = true;
            }
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

            var emailBody = $@"
                <h2>Welcome to ToggleMesh!</h2>
                <p>Please confirm your email address by clicking the link below:</p>
                <p><a href='{confirmUrl}'>Confirm Email</a></p>
            ";

            await _emailSender.SendEmailAsync(user.Email, "Confirm your ToggleMesh account", emailBody, ct);
            await Send.OkAsync(new RegisterResponse(), cancellation: ct);
            return;
        }

        var (accessToken, refreshToken) = await TokenGenerator.GenerateTokensAsync(user, _userManager, _configuration);

        if (!int.TryParse(_configuration["Auth:RefreshTokenLifetimeDays"], out var tokenLifetime))
            tokenLifetime = 7;
        
        var rt = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            Expires = DateTime.UtcNow.AddDays(tokenLifetime),
            Created = DateTime.UtcNow
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