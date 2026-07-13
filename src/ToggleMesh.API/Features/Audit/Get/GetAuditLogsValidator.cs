using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Audit.Get;

public class GetAuditLogsValidator : Validator<GetAuditLogsRequest>
{
    public GetAuditLogsValidator()
    {
        RuleFor(x => x)
            .Must(x =>
                x.ProjectId.HasValue ||
                x.EnvironmentId.HasValue)
            .WithMessage(x => "Either ProjectId or EnvironmentId must be provided.");
    }
}
