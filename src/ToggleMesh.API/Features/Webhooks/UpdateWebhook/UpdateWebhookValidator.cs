using FastEndpoints;
using FluentValidation;
using ToggleMesh.API.Infrastructure.Security;

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
            .MustAsync(async (url, ct) => await SsrfValidator.IsSafeUrlAsync(url, ct))
            .WithMessage("A valid public HTTP/HTTPS URL is required. Local or private networks are not allowed.");

        RuleForEach(x => x.Events)
            .NotEmpty().WithMessage("Event name cannot be empty.")
            .Must(evt => evt is "flag.created" or "flag.updated" or "flag.deleted" or "experiment.winner_found" or "experiment.degraded")
            .WithMessage("Invalid event. Supported events: flag.created, flag.updated, flag.deleted, experiment.winner_found, experiment.degraded.");
    }
}
