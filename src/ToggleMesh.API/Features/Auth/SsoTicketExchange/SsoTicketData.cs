namespace ToggleMesh.API.Features.Auth.SsoTicketExchange;

public class SsoTicketData
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public bool RequiresTwoFactor { get; set; }
    public string? TwoFactorToken { get; set; }
}
