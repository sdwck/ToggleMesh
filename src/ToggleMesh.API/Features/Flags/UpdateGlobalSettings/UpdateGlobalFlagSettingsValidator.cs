using FastEndpoints;
using FluentValidation;

namespace ToggleMesh.API.Features.Flags.UpdateGlobalSettings;

public class UpdateGlobalFlagSettingsValidator : Validator<UpdateGlobalFlagSettingsRequest>
{
    public UpdateGlobalFlagSettingsValidator()
    {
        RuleFor(x => x.Variations)
            .NotEmpty().WithMessage("At least one variation is required.");

        RuleFor(x => x.Variations)
            .Must(v => v == null || v.Select(x => x.Value).All(val => !string.IsNullOrWhiteSpace(val)))
            .WithMessage("Variation values cannot be empty.");
            
        RuleFor(x => x.Variations)
            .Must(v => v == null || v.Select(x => x.Value).Distinct().Count() == v.Count)
            .WithMessage("Variation values must be unique.");
    }
}
