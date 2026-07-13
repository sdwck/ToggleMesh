using FastEndpoints;
using ToggleMesh.API.Extensions;

namespace ToggleMesh.API.Infrastructure.Endpoints;

public abstract class ToggleEndpointWithoutRequest : EndpointWithoutRequest
{
    protected Guid UserId => User.GetUserId();
}

public abstract class ToggleEndpointWithoutRequest<TResponse> : EndpointWithoutRequest<TResponse>
{
    protected Guid UserId => User.GetUserId();
}
