namespace ToggleMesh.API.Features.Auth.Endpoints.SsoTicketExchange;

public class SsoTicketData
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}