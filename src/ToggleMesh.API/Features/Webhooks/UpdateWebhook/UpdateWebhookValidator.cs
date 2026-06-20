using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Webhooks.UpdateWebhook;

public class UpdateWebhookValidator : Validator<UpdateWebhookRequest>
{
    public UpdateWebhookValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Webhook name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Webhook URL is required.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var outUri) 
                         && (outUri.Scheme == Uri.UriSchemeHttp || outUri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("A valid HTTP or HTTPS URL is required.");

        RuleForEach(x => x.Events)
            .NotEmpty().WithMessage("Event name cannot be empty.")
            .Must(evt => evt is "flag.created" or "flag.updated" or "flag.deleted")
            .WithMessage("Invalid event. Supported events: flag.created, flag.updated, flag.deleted.");
    }
}
