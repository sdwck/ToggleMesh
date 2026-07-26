namespace ToggleMesh.API.Features.Analytics.Services;

public interface IPropertySanitizer
{
    object? Sanitize(object? rawProps);
}
