using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Flags.Update;

public class UpdateFlagValidator : Validator<UpdateFlagRequest>
{
    public UpdateFlagValidator()
    {
        RuleFor(x => x.RolloutPercentage)
            .InclusiveBetween(0, 100).WithMessage("Rollout percentage must be between 0 and 100.")
            .When(x => x.RolloutPercentage.HasValue);

        RuleForEach(x => x.Rules)
            .ChildRules(r =>
            {
                r.RuleFor(rule => rule.Attribute).NotEmpty().WithMessage("Rule attribute is required.");
                r.RuleFor(rule => rule.Operator).NotEmpty().WithMessage("Rule operator is required.");
                r.RuleFor(rule => rule.Value).NotEmpty().WithMessage("Rule value is required.");
            });
    }
}