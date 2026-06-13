using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Webhooks.CreateWebhook;

public class CreateWebhookValidator : Validator<CreateWebhookRequest>
{
    public CreateWebhookValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Webhook name is required.")
            .MaximumLength(128).WithMessage("Webhook name must not exceed 128 characters.");

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Webhook URL is required.")
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out var outUri) && 
                         (outUri.Scheme == Uri.UriSchemeHttp || outUri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("A valid HTTP/HTTPS URL is required.")
            .MaximumLength(2048).WithMessage("URL must not exceed 2048 characters.");

        RuleForEach(x => x.Events)
            .NotEmpty().WithMessage("Event name cannot be empty.")
            .Must(evt => evt is "flag.created" or "flag.updated" or "flag.deleted")
            .WithMessage("Invalid event. Supported events: flag.created, flag.updated, flag.deleted.");
    }
}