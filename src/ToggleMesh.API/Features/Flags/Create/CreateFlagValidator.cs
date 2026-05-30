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
    }
}