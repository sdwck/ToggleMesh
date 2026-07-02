namespace ToggleMesh.API.Features.Auth.GetTokens;

public record TokenDto(Guid Id, string Name, string Preview, DateTime CreatedAt, DateTime? ExpiresAt, DateTime? LastUsedAt);