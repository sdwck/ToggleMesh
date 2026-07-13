using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Integrations.CreateIntegration;

public class CreateIntegrationValidator : Validator<CreateIntegrationRequest>
{
    public CreateIntegrationValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.WebhookUrl)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage("WebhookUrl must be a valid absolute URI.");

        RuleFor(x => x.Provider)
            .IsInEnum();
    }
}
