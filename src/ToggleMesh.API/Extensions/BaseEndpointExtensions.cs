using FastEndpoints;

namespace ToggleMesh.API.Extensions;

public static class BaseEndpointExtensions
{
    public static void RequirePermission(this BaseEndpoint endpoint, string permission)
        => endpoint.Definition.Policies($"Permission:{permission}");
}