using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Integrations.UpdateIntegration;

public class UpdateIntegrationValidator : Validator<UpdateIntegrationRequest>
{
    public UpdateIntegrationValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);
    }
}
