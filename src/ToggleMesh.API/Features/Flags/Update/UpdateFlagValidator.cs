using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Flags.Update;

public class UpdateFlagValidator : Validator<UpdateFlagRequest>
{
    public UpdateFlagValidator()
    {
        RuleFor(x => x.FallthroughRollout)
            .Must(rollout => rollout == null || rollout.Count == 0 || rollout.Sum(r => r.Weight) == 10000)
            .WithMessage("Rollout weights must sum to exactly 100%.");

        RuleForEach(x => x.Rules)
            .ChildRules(r =>
            {
                r.RuleFor(rule => rule.Attribute).NotEmpty().WithMessage("Rule attribute is required.");
                r.RuleFor(rule => rule.Operator).NotEmpty().WithMessage("Rule operator is required.");
                r.RuleFor(rule => rule.Value).NotEmpty().WithMessage("Rule value is required.");
            });
    }
}
