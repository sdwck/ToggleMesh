using FastEndpoints;
using ToggleMesh.API.Extensions;

namespace ToggleMesh.API.Infrastructure.Endpoints;

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