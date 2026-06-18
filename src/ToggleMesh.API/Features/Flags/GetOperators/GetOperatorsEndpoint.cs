using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.Common.Rules;

namespace ToggleMesh.API.Features.Flags.GetOperators;

public class GetOperatorsEndpoint : ToggleEndpointWithoutRequest<List<string>>
{
    private readonly IEnumerable<IRuleOperator> _operators;
    
    public GetOperatorsEndpoint(IEnumerable<IRuleOperator> operators)
    {
        _operators = operators;
    }

    public override void Configure()
    {
        Get("/flags/operators");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.ProjectsView); 
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var operatorNames = _operators
            .Select(o => o.Name)
            .OrderBy(name => name)
            .ToList();

        await Send.OkAsync(operatorNames, ct);
    }
}