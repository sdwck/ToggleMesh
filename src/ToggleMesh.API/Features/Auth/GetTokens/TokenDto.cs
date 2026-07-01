namespace ToggleMesh.API.Features.Auth.Endpoints.GetTokens;

public record TokenDto(Guid Id, string Name, string Preview, DateTime CreatedAt, DateTime? ExpiresAt, DateTime? LastUsedAt);