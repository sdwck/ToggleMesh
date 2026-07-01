namespace ToggleMesh.API.Features.Auth.Endpoints.ConfirmEmail;

public class ConfirmEmailRequest
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
}
