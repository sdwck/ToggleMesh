namespace ToggleMesh.API.Features.Auth.UpdateProfile;

public record UpdateProfileRequest(string? Username = null, bool? SkipLandingPage = null);
