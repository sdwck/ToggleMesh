using FastEndpoints;
using ToggleMesh.API.Extensions;

namespace ToggleMesh.API.Infrastructure;

public abstract class ToggleEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected Guid UserId => User.GetUserId();
}

public abstract class ToggleEndpoint<TRequest> : Endpoint<TRequest>
    where TRequest : notnull
{
    protected Guid UserId => User.GetUserId();
}

public abstract class ToggleEndpointWithoutRequest<TResponse> : EndpointWithoutRequest<TResponse>
{
    protected Guid UserId => User.GetUserId();
}

public abstract class ToggleEndpointWithoutRequest : EndpointWithoutRequest
{
    protected Guid UserId => User.GetUserId();
}