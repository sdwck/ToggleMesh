using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Flags.Create;

public class CreateFlagValidator : Validator<CreateFlagRequest>
{
    public CreateFlagValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("Flag key is required.")
            .Matches("^[a-zA-Z0-9_-]+$").WithMessage("Flag key can only contain letters, numbers, underscores, and dashes.");
            
        RuleFor(x => x.FallthroughRollout)
            .Must(rollout => rollout == null || rollout.Count == 0 || rollout.Sum(r => r.Weight) == 10000)
            .WithMessage("Rollout weights must sum to exactly 100%.");
            
        RuleFor(x => x.Variations)
            .Must(v => v == null || v.Select(x => x.Value).All(val => !string.IsNullOrWhiteSpace(val)))
            .WithMessage("Variation values cannot be empty.");
    }
}
